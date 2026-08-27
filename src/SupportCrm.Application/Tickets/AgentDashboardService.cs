namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Sla;

public class AgentDashboardService(ITicketRepository ticketRepository, SlaCalculationService slaCalculationService)
{
    public async Task<IReadOnlyList<AgentDashboardTicketDto>> GetAssignedTicketsAsync(
        Guid agentId, TicketStatus? status, TicketPriority? priority, Guid? categoryId, CancellationToken ct)
    {
        var tickets = await ticketRepository.GetAssignedToAgentAsync(agentId, ct);

        // Default view is "my workload" — excludes Closed unless the agent explicitly
        // filters for it; an explicit status filter always wins over that default.
        IEnumerable<Ticket> filtered = status.HasValue
            ? tickets.Where(t => t.Status == status.Value)
            : tickets.Where(t => t.Status != TicketStatus.Closed);

        if (priority.HasValue) filtered = filtered.Where(t => t.Priority == priority.Value);
        if (categoryId.HasValue) filtered = filtered.Where(t => t.CategoryId == categoryId.Value);
        var filteredList = filtered.ToList();

        // Batch-resolve SLA status before the projection — SlaCalculationService is async and
        // cannot be called inside a synchronous LINQ .Select.
        var slaByTicket = await slaCalculationService.GetStatusesAsync(filteredList, ct);

        return filteredList
            .Select(t =>
            {
                var sla = slaByTicket.GetValueOrDefault(t.Id);
                return new AgentDashboardTicketDto(
                    t.Id, t.ReferenceNumber, t.Subject, t.Status, t.Priority, t.CategoryId, t.CreatedAtUtc,
                    sla?.ResolutionDueAtUtc ?? t.CreatedAtUtc,
                    ToSlaState(t.Status, sla));
            })
            // TicketPriority is declared Low < Medium < High < Urgent, so descending puts
            // the most severe first; SLA due-at ascending breaks ties within a priority.
            .OrderByDescending(d => d.Priority)
            .ThenBy(d => d.SlaDueAtUtc)
            .ToList();
    }

    private static string ToSlaState(TicketStatus status, TicketSlaStatusDto? sla)
    {
        if (status == TicketStatus.Closed || sla is null) return "NotApplicable";
        if (sla.IsResolutionBreached) return "Breached";
        return sla.ResolutionRemainingMinutes <= sla.ResolutionTargetMinutes * 0.2 ? "NearingBreach" : "OnTrack";
    }
}
