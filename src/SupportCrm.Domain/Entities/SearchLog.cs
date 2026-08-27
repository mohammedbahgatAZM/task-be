namespace SupportCrm.Domain.Entities;

public class SearchLog
{
    public Guid Id { get; private set; }
    public string Query { get; private set; } = default!;
    public int ResultCount { get; private set; }
    public DateTimeOffset SearchedAtUtc { get; private set; }

    private SearchLog() { } // EF Core

    public SearchLog(string query, int resultCount, DateTimeOffset searchedAtUtc)
    {
        Id = Guid.NewGuid();
        Query = query;
        ResultCount = resultCount;
        SearchedAtUtc = searchedAtUtc;
    }
}
