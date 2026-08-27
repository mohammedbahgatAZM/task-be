namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AssignmentRuleEngine(
    ITicketRepository ticketRepository,
    IAgentRepository agentRepository,
    IAssignmentRuleRepository ruleRepository,
    TicketAssignmentService assignmentService,
    AgentNotificationService notificationService)
{
    // Fixed seed id for the "General Queue" team (see SupportCrmDbContext's Team HasData) —
    // the fallback target when no active rule matches, or a skill-based rule has no available
    // skilled agent, so a ticket always lands somewhere instead of being silently unassigned.
    public static readonly Guid DefaultQueueTeamId = new("33333333-3333-3333-3333-333333333301");

    public async Task EvaluateAndAssignAsync(Guid ticketId, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var rules = await ruleRepository.GetActiveOrderedAsync(ct);
        var rule = rules.FirstOrDefault(r => r.MatchesConditions(ticket.CategoryId, ticket.Channel, ticket.Language));

        Guid? targetAgentId = null;
        Guid? targetTeamId = DefaultQueueTeamId;

        if (rule is not null)
        {
            if (rule.TargetTeamId is not null)
            {
                targetTeamId = rule.TargetTeamId;
            }
            else
            {
                var candidate = await PickLeastLoadedSkilledAgentAsync(rule.RequiredSkill!, ct);
                if (candidate is not null)
                {
                    targetAgentId = candidate.Id;
                    targetTeamId = null;
                }
                // else: no available skilled agent right now — falls through to DefaultQueueTeamId.
            }
        }

        await assignmentService.AssignAsync(ticketId, new AssignTicketRequest(targetAgentId, targetTeamId, "System"), ct);

        if (targetAgentId is not null)
        {
            await notificationService.NotifyAsync(targetAgentId.Value, "AutoAssigned",
                $"Ticket {ticket.ReferenceNumber} was auto-assigned to you.", ticketId, ct);
        }
    }

    private async Task<Agent?> PickLeastLoadedSkilledAgentAsync(string requiredSkill, CancellationToken ct)
    {
        var skilled = await agentRepository.GetBySkillAsync(requiredSkill, ct);
        var available = skilled.Where(a => a.IsAvailable).ToList();
        if (available.Count == 0) return null;

        var load = await ticketRepository.CountOpenGroupedByAgentAsync(ct);
        return available.OrderBy(a => load.GetValueOrDefault(a.Id, 0)).First();
    }
}
