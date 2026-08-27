namespace SupportCrm.Domain.Entities;

public class Customer
{
    public Guid Id { get; private set; }
    public string CustomerNumber { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Company { get; private set; }
    public string? Branch { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid? MergedIntoCustomerId { get; private set; }
    public ContactChannelType? PreferredContactChannel { get; private set; }
    public string? Address { get; private set; }
    public bool IsVip { get; private set; }
    public bool IsAtRisk { get; private set; }
    public CustomerTier Tier { get; private set; } = CustomerTier.Standard;
    public string PreferredLanguage { get; private set; } = "en"; // "en" | "ar"
    public Guid? BranchId { get; private set; } // additive, parallel to the existing Branch string above — see Platform PL-4

    private Customer() { } // EF Core

    public Customer(string customerNumber, string name, string? company, string? branch, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(customerNumber))
            throw new ArgumentException("Customer number is required.", nameof(customerNumber));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Id = Guid.NewGuid();
        CustomerNumber = customerNumber;
        Name = name;
        Company = company;
        Branch = branch;
        CreatedAtUtc = createdAtUtc;
    }

    public bool IsMerged => MergedIntoCustomerId is not null;

    public void MergeInto(Guid targetCustomerId)
    {
        if (targetCustomerId == Id)
            throw new InvalidOperationException("A customer cannot be merged into itself.");
        MergedIntoCustomerId = targetCustomerId;
    }

    public void SetPreferredContactChannel(ContactChannelType? channel) => PreferredContactChannel = channel;

    public void SetAddress(string? address) => Address = address;

    public void SetAccountFlags(bool isVip, bool isAtRisk)
    {
        IsVip = isVip;
        IsAtRisk = isAtRisk;
    }

    public void SetTier(CustomerTier tier) => Tier = tier;

    public void SetPreferredLanguage(string language) => PreferredLanguage = language is "en" or "ar" ? language : "en";

    public void SetBranch(Guid? branchId) => BranchId = branchId;

    // Platform Integrations INT-2 — the one field this prototype's ERP sync applies
    // ERP-side changes to (documented scope note: a real integration would sync a wider field set).
    public void SetCompany(string? company) => Company = company;
}
