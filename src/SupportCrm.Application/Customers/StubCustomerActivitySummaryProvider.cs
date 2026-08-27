namespace SupportCrm.Application.Customers;

public class StubCustomerActivitySummaryProvider : ICustomerActivitySummaryProvider
{
    public Task<(int OpenTicketCount, DateTimeOffset? LastInteractionAtUtc)> GetSummaryAsync(Guid customerId, CancellationToken ct)
        => Task.FromResult((0, (DateTimeOffset?)null));
}
