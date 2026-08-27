namespace SupportCrm.Application.Reports;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AgentPerformanceService(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    ITicketFeedbackRepository feedbackRepository,
    IAgentRepository agentRepository)
{
    public async Task<IReadOnlyList<AgentPerformanceDto>> GetPerformanceAsync(AgentPerformanceQuery query, CancellationToken ct)
    {
        var agents = await agentRepository.GetAllAsync(ct);
        if (query.AgentId is not null) agents = agents.Where(a => a.Id == query.AgentId).ToList();

        var allTickets = await ticketRepository.GetAllAsync(ct);
        var closedTickets = allTickets.Where(t => t.Status is TicketStatus.Resolved or TicketStatus.Closed).ToList();

        // "Over selected time periods" means when the work happened (resolution), not when the
        // ticket arrived — the date filter is applied against each ticket's resolved-at time.
        var resolvedAtByTicket = await GetResolvedAtTimesAsync(closedTickets.Select(t => t.Id).ToList(), ct);
        IEnumerable<Ticket> resolvedInRange = closedTickets.Where(t => resolvedAtByTicket.ContainsKey(t.Id));
        if (query.From is not null) resolvedInRange = resolvedInRange.Where(t => resolvedAtByTicket[t.Id] >= query.From);
        if (query.To is not null) resolvedInRange = resolvedInRange.Where(t => resolvedAtByTicket[t.Id] <= query.To);
        var resolvedList = resolvedInRange.ToList();

        var firstAgentMessageTimes = await messageRepository.GetFirstAgentMessageTimesAsync(resolvedList.Select(t => t.Id).ToList(), ct);
        var feedbackByTicket = (await feedbackRepository.GetAllAsync(ct)).ToDictionary(f => f.TicketId);

        var result = new List<AgentPerformanceDto>();
        foreach (var agent in agents)
        {
            // "Tickets handled" = currently assigned to this agent — a ticket reassigned away
            // before resolution is simply no longer in this set, satisfying the AC's exclusion
            // rule with no extra bookkeeping (see ReassignedAwayCount below for the flip side).
            var ownedResolved = resolvedList.Where(t => t.AssignedAgentId == agent.Id).ToList();

            var responseMinutes = ownedResolved
                .Where(t => firstAgentMessageTimes.ContainsKey(t.Id))
                .Select(t => (firstAgentMessageTimes[t.Id] - t.CreatedAtUtc).TotalMinutes)
                .ToList();

            var resolutionMinutes = ownedResolved
                .Select(t => (resolvedAtByTicket[t.Id] - t.CreatedAtUtc).TotalMinutes)
                .ToList();

            var ratings = ownedResolved
                .Where(t => feedbackByTicket.ContainsKey(t.Id))
                .Select(t => feedbackByTicket[t.Id].Rating)
                .ToList();

            var reassignedAwayCount = await CountReassignedAwayAsync(agent.Id, resolvedList, ct);

            result.Add(new AgentPerformanceDto(
                agent.Id, agent.Name, ownedResolved.Count,
                responseMinutes.Count > 0 ? Math.Round(responseMinutes.Average(), 1) : null,
                resolutionMinutes.Count > 0 ? Math.Round(resolutionMinutes.Average(), 1) : null,
                ratings.Count > 0 ? Math.Round(ratings.Average(), 2) : null,
                ratings.Count,
                reassignedAwayCount));
        }

        return result.OrderByDescending(a => a.TicketsResolvedCount).ToList();
    }

    // Flagged N+1 (one history query per resolved ticket) — acceptable at this app's demo scale,
    // same standard already used throughout this codebase (e.g. Customer Portal CP-2's per-ticket
    // status-history lookup).
    private async Task<Dictionary<Guid, DateTimeOffset>> GetResolvedAtTimesAsync(IReadOnlyList<Guid> ticketIds, CancellationToken ct)
    {
        var result = new Dictionary<Guid, DateTimeOffset>();
        foreach (var ticketId in ticketIds)
        {
            var history = await ticketRepository.GetStatusHistoryAsync(ticketId, ct);
            var resolvedAt = history
                .Where(h => h.NewStatus is TicketStatus.Resolved or TicketStatus.Closed)
                .OrderBy(h => h.ChangedAtUtc)
                .Select(h => h.ChangedAtUtc)
                .Cast<DateTimeOffset?>()
                .FirstOrDefault();
            if (resolvedAt is DateTimeOffset value) result[ticketId] = value;
        }
        return result;
    }

    // "Tickets you worked on that finished under someone else" — transparency, not a metric
    // correction; never folded into TicketsResolvedCount. Known limitation, flagged not fixed:
    // a ticket that ping-pongs A → B → A and resolves under A credits A fully even if B also
    // worked it — this codebase has no per-agent time-in-assignment tracking to split that fairly.
    private async Task<int> CountReassignedAwayAsync(Guid agentId, IReadOnlyList<Ticket> resolvedTicketsInRange, CancellationToken ct)
    {
        var count = 0;
        foreach (var ticket in resolvedTicketsInRange)
        {
            if (ticket.AssignedAgentId == agentId) continue; // currently theirs — already in TicketsResolvedCount, not "away"
            var history = await ticketRepository.GetAssignmentHistoryAsync(ticket.Id, ct);
            if (history.Any(h => h.OldAgentId == agentId || h.NewAgentId == agentId)) count++;
        }
        return count;
    }
}
