namespace SupportCrm.Application.Customers;

/// <summary>
/// One channel's contribution to a customer's interaction timeline (Story CM-3). Register an
/// implementation per channel (tickets, calls, chats, emails, notes, ...) via DI — none are
/// registered by default, since no such modules exist yet in this codebase. The aggregator
/// (<see cref="CustomerTimelineService"/>) works correctly with zero registered sources.
/// Each implementation is expected to apply the fromUtc/toUtc/agentName filters itself
/// (e.g. pushed down to its own storage query); the aggregator only applies the Channel
/// filter and pagination centrally, since Channel is a cross-source concept it owns.
/// </summary>
public interface ICustomerInteractionSource
{
    Task<IReadOnlyList<CustomerInteractionDto>> GetInteractionsAsync(
        Guid customerId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, string? agentName, CancellationToken ct);
}
