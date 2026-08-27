namespace SupportCrm.Domain.Entities;

public class ContactDetail
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public ContactChannelType ChannelType { get; private set; }
    public string Value { get; private set; } = default!;
    public bool IsPrimary { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private ContactDetail() { } // EF Core

    public ContactDetail(Guid customerId, ContactChannelType channelType, string value, bool isPrimary, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Contact value is required.", nameof(value));

        Id = Guid.NewGuid();
        CustomerId = customerId;
        ChannelType = channelType;
        Value = value;
        IsPrimary = isPrimary;
        CreatedAtUtc = createdAtUtc;
    }

    public void UpdateValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Contact value is required.", nameof(value));
        Value = value;
    }

    public void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;
}
