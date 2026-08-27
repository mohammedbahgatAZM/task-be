namespace SupportCrm.Domain.Entities;

public class QuickReplyTemplate
{
    public Guid Id { get; private set; }
    public string Category { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public bool IsRetired { get; private set; }
    public string CreatedBy { get; private set; } = default!;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private QuickReplyTemplate() { } // EF Core

    public QuickReplyTemplate(string category, string name, string body, string createdBy, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Template body is required.", nameof(body));

        Id = Guid.NewGuid();
        Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
        Name = name;
        Body = body;
        CreatedBy = createdBy;
        CreatedAtUtc = createdAtUtc;
    }

    public void Update(string category, string name, string body)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Template body is required.", nameof(body));

        Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
        Name = name;
        Body = body;
    }

    public void Retire() => IsRetired = true;
}
