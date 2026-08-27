namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Sla;

public class EscalationRuleEngine(
    ITicketRepository ticketRepository,
    IAgentRepository agentRepository,
    IEscalationRuleRepository escalationRuleRepository,
    SlaCalculationService slaCalculationService,
    TicketAssignmentService assignmentService,
    AgentNotificationService notificationService,
    TimeProvider timeProvider)
{
    // Scans every open ticket once. Called on a recurring interval by SlaEscalationHostedService.
    public async Task EvaluateAllAsync(CancellationToken ct)
    {
        var openTickets = await ticketRepository.GetOpenAsync(ct);
        var rules = await escalationRuleRepository.GetActiveOrderedAsync(ct);
        if (rules.Count == 0) return;

        foreach (var ticket in openTickets)
            await EvaluateTicketAsync(ticket, rules, ct);
    }

    private async Task EvaluateTicketAsync(Ticket ticket, IReadOnlyList<EscalationRule> rules, CancellationToken ct)
    {
        var rule = rules.FirstOrDefault(r => r.Matches(ticket.CategoryId, ticket.Priority));
        if (rule is null) return;

        var slaStatus = await slaCalculationService.GetStatusAsync(ticket.Id, ct);
        if (slaStatus is null) return; // no SLA target configured for this ticket — nothing to measure against

        var elapsedPercentage = 100 - slaStatus.ResolutionRemainingMinutes * 100 / Math.Max(1, slaStatus.ResolutionTargetMinutes);

        // Ascending TierNumber: catches up and fires every unfired due tier in one pass if the
        // poll interval let a ticket cross more than one threshold since the last run — a ticket
        // that's already at 95% elapsed can fire an 80% tier and a 90% tier in the same run.
        var tiers = (await escalationRuleRepository.GetTiersAsync(rule.Id, ct)).OrderBy(t => t.TierNumber);
        foreach (var tier in tiers)
        {
            if (elapsedPercentage < tier.TriggerPercentage) continue;
            if (await escalationRuleRepository.HasFiredAsync(ticket.Id, rule.Id, tier.TierNumber, ct)) continue;

            await FireTierAsync(ticket, rule, tier, ct);
        }
    }

    private async Task FireTierAsync(Ticket ticket, EscalationRule rule, EscalationTier tier, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var actions = new List<string>();

        if (tier.ReassignToAgentId is not null || tier.ReassignToTeamId is not null)
        {
            await assignmentService.AssignAsync(ticket.Id, new AssignTicketRequest(tier.ReassignToAgentId, tier.ReassignToTeamId, "System"), ct);
            actions.Add(tier.ReassignToAgentId is not null ? $"reassigned to agent {tier.ReassignToAgentId}" : $"reassigned to team {tier.ReassignToTeamId}");
        }

        if (tier.RaisePriorityTo is not null && ticket.Priority != tier.RaisePriorityTo)
        {
            ticket.SetPriority(tier.RaisePriorityTo.Value);
            actions.Add($"priority raised to {tier.RaisePriorityTo}");
        }

        if (tier.NotifySupervisor)
        {
            var supervisors = (await agentRepository.GetAllAsync(ct)).Where(a => a.IsSupervisor).ToList();
            foreach (var supervisor in supervisors)
                await notificationService.NotifyAsync(supervisor.Id, "SlaEscalation",
                    $"Ticket {ticket.ReferenceNumber} escalated (tier {tier.TierNumber}, rule '{rule.Name}').", ticket.Id, ct);
            actions.Add($"notified {supervisors.Count} supervisor(s)");
        }

        ticket.MarkEscalated(now);
        await ticketRepository.SaveChangesAsync(ct);

        await escalationRuleRepository.AddLogEntryAsync(
            new EscalationLogEntry(ticket.Id, rule.Id, tier.TierNumber, string.Join("; ", actions), now), ct);
        await escalationRuleRepository.SaveChangesAsync(ct);
    }
}
