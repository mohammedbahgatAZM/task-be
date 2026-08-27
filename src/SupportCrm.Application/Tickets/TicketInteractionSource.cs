namespace SupportCrm.Application.Tickets;

using SupportCrm.Application.Customers;
using SupportCrm.Domain.Repositories;

public class TicketInteractionSource(ITicketRepository ticketRepository) : ICustomerInteractionSource
{
    public async Task<IReadOnlyList<CustomerInteractionDto>> GetInteractionsAsync(
        Guid customerId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, string? agentName, CancellationToken ct)
    {
        var tickets = await ticketRepository.GetByCustomerAsync(customerId, ct);

        return tickets
            .Where(t => fromUtc is null || t.CreatedAtUtc >= fromUtc)
            .Where(t => toUtc is null || t.CreatedAtUtc <= toUtc)
            .Select(t => new CustomerInteractionDto(t.Id, "Ticket", t.CreatedAtUtc, $"{t.ReferenceNumber}: {t.Subject}", null, $"/tickets/{t.Id}"))
            .ToList();
    }
}
