namespace SupportCrm.Application.Ai;

using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Tickets;

public class TicketSummaryService(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    ITicketAiSummaryRepository summaryRepository,
    IAiSummaryProvider summaryProvider,
    TimeProvider timeProvider)
{
    public async Task<TicketAiSummaryDto?> GetAsync(Guid ticketId, CancellationToken ct)
    {
        var summary = await summaryRepository.GetByTicketAsync(ticketId, ct);
        return summary is null ? null : ToDto(summary);
    }

    public async Task<TicketAiSummaryDto> GenerateAsync(Guid ticketId, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundForAiException(ticketId.ToString());
        var messages = await messageRepository.GetMessagesAsync(ticketId, ct);
        var notes = await messageRepository.GetNotesAsync(ticketId, ct);

        var summaryText = summaryProvider.Summarize(ticket, messages, notes);
        var now = timeProvider.GetUtcNow();

        var existing = await summaryRepository.GetByTicketAsync(ticketId, ct);
        if (existing is null)
        {
            var created = new Domain.Entities.TicketAiSummary(ticketId, summaryText, messages.Count, now);
            await summaryRepository.AddAsync(created, ct);
            await summaryRepository.SaveChangesAsync(ct);
            return ToDto(created);
        }

        existing.Regenerate(summaryText, messages.Count, now);
        await summaryRepository.SaveChangesAsync(ct);
        return ToDto(existing);
    }

    private static TicketAiSummaryDto ToDto(Domain.Entities.TicketAiSummary s) => new(s.TicketId, s.SummaryText, s.SourceMessageCount, s.GeneratedAtUtc);
}
