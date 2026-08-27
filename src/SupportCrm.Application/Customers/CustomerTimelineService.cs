namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Repositories;

public class CustomerTimelineService(ICustomerRepository customerRepository, IEnumerable<ICustomerInteractionSource> sources)
{
    public async Task<CustomerTimelinePageDto> GetTimelineAsync(Guid customerId, CustomerTimelineQuery query, CancellationToken ct)
    {
        _ = await customerRepository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 200 ? 50 : query.PageSize;

        var perSourceResults = await Task.WhenAll(
            sources.Select(s => s.GetInteractionsAsync(customerId, query.FromUtc, query.ToUtc, query.AgentName, ct)));

        var merged = perSourceResults
            .SelectMany(r => r)
            .Where(i => query.Channel is null || string.Equals(i.Channel, query.Channel, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(i => i.OccurredAtUtc)
            .ToList();

        var pageItems = merged.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new CustomerTimelinePageDto(pageItems, page, pageSize, merged.Count);
    }
}
