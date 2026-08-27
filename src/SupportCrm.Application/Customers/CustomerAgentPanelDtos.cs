namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Entities;

public record CustomerPastTicketDto(Guid Id, string ReferenceNumber, string Subject, TicketStatus Status);

public record CustomerAgentPanelDto(
    CustomerDto Customer,
    IReadOnlyList<ContactDetailDto> ContactDetails,
    int OpenTicketCount,
    IReadOnlyList<CustomerPastTicketDto> PastTickets,
    bool IsSensitiveDataMasked);
