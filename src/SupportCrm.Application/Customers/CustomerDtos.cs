namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Entities;

public record CreateCustomerRequest(string Name, string? Company, string? Branch);

public record CustomerDto(
    Guid Id,
    string CustomerNumber,
    string Name,
    string? Company,
    string? Branch,
    DateTimeOffset CreatedAtUtc,
    string? Address,
    ContactChannelType? PreferredContactChannel,
    bool IsVip,
    bool IsAtRisk,
    string PreferredLanguage = "en",
    Guid? BranchId = null);

public record CustomerSummaryDto(
    CustomerDto Customer,
    int OpenTicketCount,
    DateTimeOffset? LastInteractionAtUtc);

public record DuplicateCandidateDto(CustomerDto Customer, double Score);

public record MergeCustomersRequest(Guid SourceCustomerId, Guid TargetCustomerId);

public record SetCustomerAccountFlagsRequest(bool IsVip, bool IsAtRisk);
public record SetCustomerLanguageRequest(string Language);
public record SetCustomerBranchRequest(Guid? BranchId);
public record SetCustomerCompanyRequest(string? Company);
