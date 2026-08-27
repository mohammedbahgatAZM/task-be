namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketMessageService(ITicketRepository ticketRepository, ITicketMessageRepository repository, TimeProvider timeProvider)
{
    public async Task<TicketMessageDto> AddMessageAsync(Guid ticketId, AddTicketMessageRequest request, CancellationToken ct)
    {
        _ = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var message = new TicketMessage(ticketId, request.Body.Trim(), request.AuthorName, request.AuthorKind, timeProvider.GetUtcNow());
        await repository.AddMessageAsync(message, ct);
        await repository.SaveChangesAsync(ct);
        return new TicketMessageDto(message.Id, message.Body, message.AuthorName, message.AuthorKind, message.CreatedAtUtc);
    }

    public async Task<TicketNoteDto> AddNoteAsync(Guid ticketId, AddTicketNoteRequest request, CancellationToken ct)
    {
        _ = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var note = new TicketNote(ticketId, request.Text.Trim(), request.AuthorName, timeProvider.GetUtcNow());
        await repository.AddNoteAsync(note, ct);
        await repository.SaveChangesAsync(ct);
        return new TicketNoteDto(note.Id, note.Text, note.AuthorName, note.CreatedAtUtc);
    }
}
