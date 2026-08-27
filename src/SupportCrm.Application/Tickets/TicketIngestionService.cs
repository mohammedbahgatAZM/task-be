namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

/// <summary>
/// Shared, channel-agnostic entry point every Communication Channels story's inbound
/// webhook calls. Resolves the customer, then reuses an existing OPEN ticket for that
/// customer instead of always creating a new one — this is what makes "switching channels
/// mid-conversation does not create a duplicate ticket" true by construction.
/// </summary>
public class TicketIngestionService(
    ITicketRepository ticketRepository,
    TicketService ticketService,
    TicketCustomerResolver customerResolver,
    ITicketMessageRepository messageRepository,
    TimeProvider timeProvider)
{
    public async Task<Ticket> IngestInboundMessageAsync(IngestInboundMessageRequest request, CancellationToken ct)
    {
        var customerId = await customerResolver.ResolveCustomerIdAsync(request.RequesterName, request.RequesterContactValue, ct);
        var ticket = await ticketRepository.FindOpenTicketForCustomerAsync(customerId, ct);

        if (ticket is null)
        {
            var created = await ticketService.CreateAsync(
                new CreateTicketRequest(request.Channel, request.Subject, request.Body, request.RequesterName, request.RequesterContactValue, "System"), ct);
            ticket = await ticketRepository.GetByIdAsync(created.Id, ct);
        }

        var message = new TicketMessage(ticket!.Id, request.Body, request.RequesterName, "Customer", timeProvider.GetUtcNow());
        message.SetChannel(request.Channel);
        await messageRepository.AddMessageAsync(message, ct);
        await messageRepository.SaveChangesAsync(ct);

        return ticket;
    }
}
