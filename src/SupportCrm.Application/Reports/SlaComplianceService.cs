namespace SupportCrm.Application.Reports;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Sla;

public class SlaComplianceService(
    ITicketRepository ticketRepository,
    ICustomerRepository customerRepository,
    ITicketMessageRepository messageRepository,
    ITeamRepository teamRepository,
    IAgentRepository agentRepository,
    ITicketCategoryRepository categoryRepository,
    SlaTargetService targetService,
    SlaCalculationService slaCalculationService,
    BusinessCalendarService calendarService)
{
    // Design note: breakdowns (ByTeam/ByAgent/ByCategory/ByPriority) report RESOLUTION-SLA
    // compliance specifically — the metric a support manager most commonly tracks day to day.
    // The overall response-vs-resolution split is still available at the top level.
    private record TicketSlaEvaluation(
        Guid TicketId, DateTimeOffset CreatedAtUtc, Guid? TeamId, Guid? AgentId, Guid? CategoryId, TicketPriority Priority,
        bool HasPolicy, bool? ResponseMet, bool? ResolutionMet);

    public async Task<SlaComplianceReportDto> GetComplianceReportAsync(SlaComplianceReportQuery query, CancellationToken ct)
    {
        var all = await ticketRepository.GetAllAsync(ct);
        IEnumerable<Ticket> filtered = all;
        if (query.From is not null) filtered = filtered.Where(t => t.CreatedAtUtc >= query.From);
        if (query.To is not null) filtered = filtered.Where(t => t.CreatedAtUtc <= query.To);
        if (query.TeamId is not null) filtered = filtered.Where(t => t.AssignedTeamId == query.TeamId);
        if (query.AgentId is not null) filtered = filtered.Where(t => t.AssignedAgentId == query.AgentId);
        if (query.CategoryId is not null) filtered = filtered.Where(t => t.CategoryId == query.CategoryId);
        if (query.Priority is not null) filtered = filtered.Where(t => t.Priority == query.Priority);
        var tickets = filtered.ToList();

        var evaluations = await EvaluateTicketsAsync(tickets, ct);
        var evaluated = evaluations.Where(e => e.HasPolicy).ToList();
        var noPolicyCount = evaluations.Count - evaluated.Count;
        var inProgressCount = evaluated.Count(e => e.ResponseMet is null && e.ResolutionMet is null);

        var responseJudged = evaluated.Where(e => e.ResponseMet is not null).ToList();
        var resolutionJudged = evaluated.Where(e => e.ResolutionMet is not null).ToList();

        var responseCompliance = Percentage(responseJudged, e => e.ResponseMet == true);
        var resolutionCompliance = Percentage(resolutionJudged, e => e.ResolutionMet == true);

        var teamsById = (await teamRepository.GetAllAsync(ct)).ToDictionary(t => t.Id, t => t.Name);
        var agentsById = (await agentRepository.GetAllAsync(ct)).ToDictionary(a => a.Id, a => a.Name);
        var categoriesById = (await categoryRepository.GetAllAsync(ct)).ToDictionary(c => c.Id, c => c.Name);

        var trend = resolutionJudged
            .GroupBy(e => TicketReportService.BucketStart(e.CreatedAtUtc, ReportGranularity.Weekly))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var responseGroup = responseJudged.Where(r => TicketReportService.BucketStart(r.CreatedAtUtc, ReportGranularity.Weekly) == g.Key).ToList();
                return new SlaCompliancePointDto(g.Key, g.Count(), Percentage(responseGroup, e => e.ResponseMet == true), Percentage(g.ToList(), e => e.ResolutionMet == true));
            })
            .ToList();

        return new SlaComplianceReportDto(
            evaluated.Count, inProgressCount, noPolicyCount,
            responseCompliance, resolutionCompliance,
            BuildBreakdown(resolutionJudged, e => e.TeamId is Guid teamId && teamsById.TryGetValue(teamId, out var name) ? name : "Unassigned"),
            BuildBreakdown(resolutionJudged, e => e.AgentId is Guid agentId && agentsById.TryGetValue(agentId, out var name) ? name : "Unassigned"),
            BuildBreakdown(resolutionJudged, e => e.CategoryId is Guid categoryId && categoriesById.TryGetValue(categoryId, out var name) ? name : "Uncategorized"),
            BuildBreakdown(resolutionJudged, e => e.Priority.ToString()),
            trend);
    }

    private async Task<List<TicketSlaEvaluation>> EvaluateTicketsAsync(List<Ticket> tickets, CancellationToken ct)
    {
        var result = new List<TicketSlaEvaluation>();
        var customersById = (await customerRepository.GetAllAsync(ct)).ToDictionary(c => c.Id);

        var openTickets = tickets.Where(t => t.Status is not (TicketStatus.Resolved or TicketStatus.Closed)).ToList();
        var closedTickets = tickets.Where(t => t.Status is TicketStatus.Resolved or TicketStatus.Closed).ToList();

        // Open tickets: literally the same calculation the ticket's own SLA status card uses.
        var openStatuses = await slaCalculationService.GetStatusesAsync(openTickets, ct);
        foreach (var ticket in openTickets)
        {
            if (!openStatuses.TryGetValue(ticket.Id, out var status))
            {
                result.Add(NoPolicy(ticket));
                continue;
            }
            result.Add(new TicketSlaEvaluation(
                ticket.Id, ticket.CreatedAtUtc, ticket.AssignedTeamId, ticket.AssignedAgentId, ticket.CategoryId, ticket.Priority,
                HasPolicy: true,
                ResponseMet: status.IsResponseBreached ? false : null,   // null = not yet due, not yet evaluable
                ResolutionMet: status.IsResolutionBreached ? false : null));
        }

        // Closed tickets: SlaCalculationService can't answer this (it only ever compares against
        // "now") — compute actual elapsed business time to the real response/resolution event.
        if (closedTickets.Count > 0)
        {
            var firstAgentMessageTimes = await messageRepository.GetFirstAgentMessageTimesAsync(closedTickets.Select(t => t.Id).ToList(), ct);
            foreach (var ticket in closedTickets)
            {
                var tier = customersById.TryGetValue(ticket.CustomerId, out var c) ? c.Tier : CustomerTier.Standard;
                var target = await targetService.ResolveAsync(ticket.Priority, ticket.CategoryId, tier, ct);
                if (target is null) { result.Add(NoPolicy(ticket)); continue; }

                var history = (await ticketRepository.GetStatusHistoryAsync(ticket.Id, ct)).OrderBy(h => h.ChangedAtUtc).ToList();
                var resolvedAt = history.FirstOrDefault(h => h.NewStatus is TicketStatus.Resolved or TicketStatus.Closed)?.ChangedAtUtc;

                bool responseMet;
                if (firstAgentMessageTimes.TryGetValue(ticket.Id, out var firstResponseAt))
                {
                    var elapsed = await calendarService.CalculateBusinessMinutesBetweenAsync(ticket.CreatedAtUtc, firstResponseAt, ct);
                    var pause = await PendingBusinessMinutesUpToAsync(history, firstResponseAt, ct);
                    responseMet = (elapsed - pause) <= target.ResponseTargetMinutes;
                }
                else
                {
                    responseMet = false; // resolved with zero agent replies — never met a response target
                }

                bool resolutionMet;
                if (resolvedAt is DateTimeOffset resolvedAtValue)
                {
                    var elapsed = await calendarService.CalculateBusinessMinutesBetweenAsync(ticket.CreatedAtUtc, resolvedAtValue, ct);
                    var pause = await PendingBusinessMinutesUpToAsync(history, resolvedAtValue, ct);
                    resolutionMet = (elapsed - pause) <= target.ResolutionTargetMinutes;
                }
                else
                {
                    resolutionMet = false; // defensive only — unreachable: closedTickets are Resolved/Closed by construction
                }

                result.Add(new TicketSlaEvaluation(ticket.Id, ticket.CreatedAtUtc, ticket.AssignedTeamId, ticket.AssignedAgentId, ticket.CategoryId, ticket.Priority, true, responseMet, resolutionMet));
            }
        }

        return result;
    }

    // Shaped like SlaCalculationService's own private GetPendingBusinessMinutesAsync, but bounded
    // to a historical event instead of "now" — kept as a separate method rather than parameterizing
    // the shipped real-time one, to avoid touching SLA & Automation's already-verified calculation.
    private async Task<int> PendingBusinessMinutesUpToAsync(IReadOnlyList<TicketStatusChangeEntry> orderedHistory, DateTimeOffset asOf, CancellationToken ct)
    {
        var total = 0;
        for (var i = 0; i < orderedHistory.Count; i++)
        {
            if (orderedHistory[i].NewStatus != TicketStatus.Pending) continue;
            var from = orderedHistory[i].ChangedAtUtc;
            if (from >= asOf) continue;
            var to = i + 1 < orderedHistory.Count ? orderedHistory[i + 1].ChangedAtUtc : asOf;
            if (to > asOf) to = asOf;
            total += await calendarService.CalculateBusinessMinutesBetweenAsync(from, to, ct);
        }
        return total;
    }

    private static TicketSlaEvaluation NoPolicy(Ticket t) =>
        new(t.Id, t.CreatedAtUtc, t.AssignedTeamId, t.AssignedAgentId, t.CategoryId, t.Priority, false, null, null);

    private static double Percentage(List<TicketSlaEvaluation> judged, Func<TicketSlaEvaluation, bool> metPredicate) =>
        judged.Count == 0 ? 0 : Math.Round(100.0 * judged.Count(metPredicate) / judged.Count, 1);

    private static List<SlaBreakdownDto> BuildBreakdown(List<TicketSlaEvaluation> resolutionJudged, Func<TicketSlaEvaluation, string> keySelector) =>
        resolutionJudged
            .GroupBy(keySelector)
            .Select(g => new SlaBreakdownDto(g.Key, g.Count(), g.Count(e => e.ResolutionMet == false), Percentage(g.ToList(), e => e.ResolutionMet == true)))
            .OrderBy(b => b.Key)
            .ToList();
}
