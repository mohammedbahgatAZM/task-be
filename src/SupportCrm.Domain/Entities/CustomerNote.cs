namespace SupportCrm.Domain.Entities;

public class CustomerNote
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Text { get; private set; } = default!;
    public string AuthorName { get; private set; } = default!;
    public bool IsPinned { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private CustomerNote() { } // EF Core

    public CustomerNote(Guid customerId, string text, string authorName, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Note text is required.", nameof(text));

        Id = Guid.NewGuid();
        CustomerId = customerId;
        Text = text;
        AuthorName = string.IsNullOrWhiteSpace(authorName) ? "unknown" : authorName;
        CreatedAtUtc = createdAtUtc;
    }

    public void SetPinned(bool isPinned) => IsPinned = isPinned;
}
