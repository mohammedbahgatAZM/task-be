namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

/// <summary>
/// Masks Customer.Address and every ContactDetail.Value server-side when the requesting
/// agent lacks CanViewSensitiveData — the client never decides this itself. There is no
/// auth middleware in this app, so "the requesting agent" arrives as an explicit id
/// (same "acting as" pattern as Agent Dashboard AD-1), and this service looks up *that*
/// agent's own flag rather than trusting anything the caller claims about its own
/// permissions.
/// </summary>
public class CustomerAgentPanelService(
    ICustomerRepository customerRepository,
    IContactDetailRepository contactDetailRepository,
    ITicketRepository ticketRepository,
    IAgentRepository agentRepository)
{
    private const string MaskedPlaceholder = "•••• (restricted)";

    public async Task<CustomerAgentPanelDto> GetPanelAsync(Guid customerId, Guid requestingAgentId, CancellationToken ct)
    {
        var customer = await customerRepository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);
        var requestingAgent = await agentRepository.GetByIdAsync(requestingAgentId, ct);
        var canView = requestingAgent?.CanViewSensitiveData ?? false;

        var contactDetails = await contactDetailRepository.GetByCustomerAsync(customerId, ct);
        var tickets = await ticketRepository.GetByCustomerAsync(customerId, ct);

        var customerDto = new CustomerDto(
            customer.Id, customer.CustomerNumber, customer.Name, customer.Company, customer.Branch, customer.CreatedAtUtc,
            Mask(customer.Address, canView), customer.PreferredContactChannel, customer.IsVip, customer.IsAtRisk);

        var contactDetailDtos = contactDetails
            .Select(c => new ContactDetailDto(c.Id, c.ChannelType, canView ? c.Value : MaskedPlaceholder, c.IsPrimary, c.CreatedAtUtc))
            .ToList();

        var openStatuses = new[] { TicketStatus.New, TicketStatus.Open, TicketStatus.Pending };
        var pastTickets = tickets
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(10)
            .Select(t => new CustomerPastTicketDto(t.Id, t.ReferenceNumber, t.Subject, t.Status))
            .ToList();

        return new CustomerAgentPanelDto(
            customerDto, contactDetailDtos, tickets.Count(t => openStatuses.Contains(t.Status)), pastTickets, !canView);
    }

    private static string? Mask(string? value, bool canView) =>
        canView || value is null ? value : MaskedPlaceholder;
}
