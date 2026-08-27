namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IChatRepository
{
    Task<ChatSession?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ChatSession>> GetQueuedAsync(CancellationToken ct);
    Task<int> CountQueuedAheadOfAsync(DateTimeOffset startedAtUtc, CancellationToken ct);
    Task AddAsync(ChatSession session, CancellationToken ct);
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid chatSessionId, CancellationToken ct);
    Task AddMessageAsync(ChatMessage message, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
