namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketCollaborationService(
    ITicketCollaboratorRepository collaboratorRepository,
    IAgentRepository agentRepository,
    AgentNotificationService notificationService,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<TicketCollaboratorDto>> GetForTicketAsync(Guid ticketId, CancellationToken ct) =>
        (await collaboratorRepository.GetByTicketAsync(ticketId, ct))
            .Select(c => new TicketCollaboratorDto(c.Id, c.TicketId, c.AgentId, c.AddedAtUtc))
            .ToList();

    public async Task AddCollaboratorAsync(Guid ticketId, Guid agentId, CancellationToken ct)
    {
        if (await collaboratorRepository.ExistsAsync(ticketId, agentId, ct)) return; // idempotent — already a collaborator
        await collaboratorRepository.AddAsync(new TicketCollaborator(ticketId, agentId, timeProvider.GetUtcNow()), ct);
        await collaboratorRepository.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Called after an internal note is saved. Matching is deliberately simple — an
    /// exact, case-insensitive "@FullName" substring check against every known agent's
    /// name, not a regex tokenizer or fuzzy match — there is no mention-autocomplete UI,
    /// so the note text is always a plain string typed by the agent.
    /// </summary>
    public async Task ProcessMentionsAsync(Guid ticketId, string noteText, CancellationToken ct)
    {
        var agents = await agentRepository.GetAllAsync(ct);
        var mentioned = agents.Where(a => noteText.Contains($"@{a.Name}", StringComparison.OrdinalIgnoreCase));

        foreach (var agent in mentioned)
        {
            await notificationService.NotifyAsync(agent.Id, "Mention", "You were mentioned in an internal note.", ticketId, ct);
            await AddCollaboratorAsync(ticketId, agent.Id, ct);
        }
    }
}
