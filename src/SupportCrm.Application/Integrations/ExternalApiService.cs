namespace SupportCrm.Application.Integrations;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Customers;
using SupportCrm.Application.Tickets;

// INT-1 — backs api/integrations/v1/*. Reads go straight to the repositories and project onto
// the small External*Dto contract; writes are reused through CustomerService/TicketService
// rather than duplicated, so an externally-created ticket still gets AI categorization,
// department routing, assignment-rule evaluation, and the ticket.created webhook dispatch —
// exactly the same as one created from the agent UI.
public class ExternalApiService(
    ICustomerRepository customerRepository,
    ITicketRepository ticketRepository,
    IAgentRepository agentRepository,
    CustomerService customerService,
    TicketService ticketService)
{
    public async Task<IReadOnlyList<ExternalCustomerDto>> GetCustomersAsync(CancellationToken ct) =>
        (await customerRepository.GetAllAsync(ct)).Select(ToDto).ToList();

    public async Task<ExternalCustomerDto?> GetCustomerAsync(Guid id, CancellationToken ct)
    {
        var customer = await customerRepository.GetByIdAsync(id, ct);
        return customer is null ? null : ToDto(customer);
    }

    public async Task<ExternalCustomerDto> CreateCustomerAsync(ExternalCreateCustomerRequest request, CancellationToken ct)
    {
        var created = await customerService.CreateAsync(new CreateCustomerRequest(request.Name, request.Company, null), ct);
        return new ExternalCustomerDto(created.Id, created.CustomerNumber, created.Name, created.Company, created.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<ExternalTicketDto>> GetTicketsAsync(CancellationToken ct) =>
        (await ticketRepository.GetAllAsync(ct)).Select(ToDto).ToList();

    public async Task<ExternalTicketDto?> GetTicketAsync(Guid id, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(id, ct);
        return ticket is null ? null : ToDto(ticket);
    }

    public async Task<ExternalTicketDto> CreateTicketAsync(ExternalCreateTicketRequest request, CancellationToken ct)
    {
        var created = await ticketService.CreateAsync(new CreateTicketRequest(
            TicketChannel.Manual, request.Subject, request.Description, request.RequesterName,
            request.RequesterContactValue, "Integrations API", null, request.CustomerId), ct);
        return new ExternalTicketDto(created.Id, created.ReferenceNumber, created.CustomerId, created.Channel, created.Subject, created.Status, created.Priority, created.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<ExternalUserDto>> GetUsersAsync(CancellationToken ct) =>
        (await agentRepository.GetAllAsync(ct)).Select(a => new ExternalUserDto(a.Id, a.Name, a.IsAvailable)).ToList();

    private static ExternalCustomerDto ToDto(Customer c) => new(c.Id, c.CustomerNumber, c.Name, c.Company, c.CreatedAtUtc);
    private static ExternalTicketDto ToDto(Ticket t) => new(t.Id, t.ReferenceNumber, t.CustomerId, t.Channel, t.Subject, t.Status, t.Priority, t.CreatedAtUtc);
}
