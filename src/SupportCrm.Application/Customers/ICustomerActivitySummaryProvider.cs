namespace SupportCrm.Application.Customers;

/// <summary>
/// Supplies the "open tickets" / "last interaction" figures shown on a customer's profile summary.
/// No Ticketing or interaction-history module exists yet in this codebase; register
/// <see cref="StubCustomerActivitySummaryProvider"/> until one does.
/// </summary>
public interface ICustomerActivitySummaryProvider
{
    Task<(int OpenTicketCount, DateTimeOffset? LastInteractionAtUtc)> GetSummaryAsync(Guid customerId, CancellationToken ct);
}
