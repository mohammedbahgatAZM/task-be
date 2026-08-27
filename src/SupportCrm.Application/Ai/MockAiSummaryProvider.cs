namespace SupportCrm.Application.Ai;

using SupportCrm.Domain.Entities;

public class MockAiSummaryProvider : IAiSummaryProvider
{
    public string Summarize(Ticket ticket, IReadOnlyList<TicketMessage> messages, IReadOnlyList<TicketNote> notes)
    {
        var ordered = messages.OrderBy(m => m.CreatedAtUtc).ToList();
        var firstCustomerMessage = ordered.FirstOrDefault(m => m.AuthorKind == "Customer");
        var agentReplyCount = ordered.Count(m => m.AuthorKind == "Agent");

        var issue = firstCustomerMessage is not null
            ? Truncate(firstCustomerMessage.Body, 240)
            : Truncate(ticket.Description ?? ticket.Subject, 240);

        return $"Customer issue: {issue} " +
               $"Agent activity: {agentReplyCount} repl{(agentReplyCount == 1 ? "y" : "ies")} so far, {notes.Count} internal note(s). " +
               $"Current status: {ticket.Status} (priority {ticket.Priority}).";
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
}
