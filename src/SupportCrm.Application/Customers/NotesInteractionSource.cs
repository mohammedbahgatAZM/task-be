namespace SupportCrm.Application.Customers;

public class NotesInteractionSource(INoteAndAttachmentRepository repository) : ICustomerInteractionSource
{
    public async Task<IReadOnlyList<CustomerInteractionDto>> GetInteractionsAsync(
        Guid customerId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, string? agentName, CancellationToken ct)
    {
        var notes = await repository.GetNotesAsync(customerId, ct);

        return notes
            .Where(n => fromUtc is null || n.CreatedAtUtc >= fromUtc)
            .Where(n => toUtc is null || n.CreatedAtUtc <= toUtc)
            .Where(n => agentName is null || string.Equals(n.AuthorName, agentName, StringComparison.OrdinalIgnoreCase))
            .Select(n => new CustomerInteractionDto(n.Id, "Note", n.CreatedAtUtc, n.Text, n.AuthorName, null))
            .ToList();
    }
}
