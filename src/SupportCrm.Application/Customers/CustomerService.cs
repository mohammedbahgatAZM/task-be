namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class CustomerService(ICustomerRepository repository, ICustomerActivitySummaryProvider activitySummaryProvider, TimeProvider timeProvider)
{
    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.", nameof(request));

        var customerNumber = $"CUST-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var customer = new Customer(customerNumber, request.Name.Trim(), request.Company?.Trim(), request.Branch?.Trim(), timeProvider.GetUtcNow());

        await repository.AddAsync(customer, ct);
        await repository.SaveChangesAsync(ct);

        return ToDto(customer);
    }

    public async Task<CustomerDto?> GetByCustomerNumberAsync(string customerNumber, CancellationToken ct)
    {
        var customer = await repository.GetByCustomerNumberAsync(customerNumber, ct);
        return customer is null ? null : ToDto(customer);
    }

    public async Task<CustomerSummaryDto> GetSummaryAsync(Guid id, CancellationToken ct)
    {
        var customer = await repository.GetByIdAsync(id, ct) ?? throw new CustomerNotFoundException(id);
        var (openTicketCount, lastInteractionAtUtc) = await activitySummaryProvider.GetSummaryAsync(id, ct);
        return new CustomerSummaryDto(ToDto(customer), openTicketCount, lastInteractionAtUtc);
    }

    public async Task<IReadOnlyList<DuplicateCandidateDto>> FindDuplicatesAsync(string name, string? company, CancellationToken ct)
    {
        var candidates = await repository.SearchAsync(name, take: 10, ct);
        return candidates
            .Where(c => !c.IsMerged)
            .Select(c => new DuplicateCandidateDto(ToDto(c), ScoreMatch(c, name, company)))
            .Where(d => d.Score > 0)
            .OrderByDescending(d => d.Score)
            .ToList();
    }

    public async Task MergeAsync(MergeCustomersRequest request, CancellationToken ct)
    {
        if (request.SourceCustomerId == request.TargetCustomerId)
            throw new ArgumentException("Source and target customer must differ.", nameof(request));

        var source = await repository.GetByIdAsync(request.SourceCustomerId, ct) ?? throw new CustomerNotFoundException(request.SourceCustomerId);
        _ = await repository.GetByIdAsync(request.TargetCustomerId, ct) ?? throw new CustomerNotFoundException(request.TargetCustomerId);

        source.MergeInto(request.TargetCustomerId);
        await repository.SaveChangesAsync(ct);
    }

    public async Task SetAccountFlagsAsync(Guid customerId, SetCustomerAccountFlagsRequest request, CancellationToken ct)
    {
        var customer = await repository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);
        customer.SetAccountFlags(request.IsVip, request.IsAtRisk);
        await repository.SaveChangesAsync(ct);
    }

    public async Task SetPreferredLanguageAsync(Guid customerId, string language, CancellationToken ct)
    {
        var customer = await repository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);
        customer.SetPreferredLanguage(language);
        await repository.SaveChangesAsync(ct);
    }

    public async Task SetBranchAsync(Guid customerId, Guid? branchId, CancellationToken ct)
    {
        var customer = await repository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);
        customer.SetBranch(branchId);
        await repository.SaveChangesAsync(ct);
    }

    // Platform Integrations INT-2 — an agent-side edit to Company is what ErpSyncService's
    // conflict check compares against the simulated ERP-side value; without a way to edit
    // Company at all, the "sync conflicts are flagged" AC would only ever be reachable from the
    // ERP side, never genuinely bi-directionally.
    public async Task SetCompanyAsync(Guid customerId, string? company, CancellationToken ct)
    {
        var customer = await repository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);
        customer.SetCompany(company?.Trim());
        await repository.SaveChangesAsync(ct);
    }

    private static double ScoreMatch(Customer candidate, string name, string? company)
    {
        var score = 0.0;
        if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)) score += 0.7;
        else if (candidate.Name.Contains(name, StringComparison.OrdinalIgnoreCase)) score += 0.3;
        if (!string.IsNullOrWhiteSpace(company) && string.Equals(candidate.Company, company, StringComparison.OrdinalIgnoreCase)) score += 0.3;
        return score;
    }

    private static CustomerDto ToDto(Customer c) => new(
        c.Id, c.CustomerNumber, c.Name, c.Company, c.Branch, c.CreatedAtUtc, c.Address, c.PreferredContactChannel, c.IsVip, c.IsAtRisk,
        c.PreferredLanguage, c.BranchId);
}
