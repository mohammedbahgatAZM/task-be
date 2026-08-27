namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Repositories;

/// <summary>
/// Replaces <see cref="StubCustomerActivitySummaryProvider"/> (registered by Customer
/// Management's CM-1, before any real interaction data existed). "Open tickets" comes
/// from the ticket repository directly; "last interaction" is computed the same way the
/// CM-3 timeline is — by asking every registered <see cref="ICustomerInteractionSource"/>
/// for its most recent entry — so this provider never needs to change again as new
/// interaction sources are added.
/// </summary>
public class CustomerActivitySummaryProvider(
    ITicketRepository ticketRepository,
    IEnumerable<ICustomerInteractionSource> interactionSources) : ICustomerActivitySummaryProvider
{
    public async Task<(int OpenTicketCount, DateTimeOffset? LastInteractionAtUtc)> GetSummaryAsync(Guid customerId, CancellationToken ct)
    {
        var openTicketCount = await ticketRepository.CountOpenByCustomerAsync(customerId, ct);

        var perSourceResults = await Task.WhenAll(
            interactionSources.Select(s => s.GetInteractionsAsync(customerId, null, null, null, ct)));
        var lastInteractionAtUtc = perSourceResults
            .SelectMany(r => r)
            .Select(i => (DateTimeOffset?)i.OccurredAtUtc)
            .Max();

        return (openTicketCount, lastInteractionAtUtc);
    }
}
