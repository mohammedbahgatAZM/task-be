namespace SupportCrm.Application.Customers;

public record CustomerTimelineQuery(
    string? Channel,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? AgentName,
    int Page = 1,
    int PageSize = 50);

public record CustomerTimelinePageDto(IReadOnlyList<CustomerInteractionDto> Items, int Page, int PageSize, int TotalCount);
