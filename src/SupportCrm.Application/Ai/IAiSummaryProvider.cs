namespace SupportCrm.Application.Ai;

using SupportCrm.Domain.Entities;

/// <summary>
/// Produces a ticket summary. No real LLM exists in this codebase — register
/// <see cref="MockAiSummaryProvider"/> until one does. That implementation is
/// extractive/heuristic (picks the first customer message, counts agent replies,
/// states current status) — it does not call any external AI service.
/// </summary>
public interface IAiSummaryProvider
{
    string Summarize(Ticket ticket, IReadOnlyList<TicketMessage> messages, IReadOnlyList<TicketNote> notes);
}
