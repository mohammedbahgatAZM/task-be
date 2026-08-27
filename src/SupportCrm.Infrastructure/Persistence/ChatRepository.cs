namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ChatRepository(SupportCrmDbContext dbContext) : IChatRepository
{
    public Task<ChatSession?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.ChatSessions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<ChatSession>> GetQueuedAsync(CancellationToken ct) =>
        await dbContext.ChatSessions.Where(s => s.Status == ChatSessionStatus.Queued).ToListAsync(ct);

    public Task<int> CountQueuedAheadOfAsync(DateTimeOffset startedAtUtc, CancellationToken ct) =>
        dbContext.ChatSessions.CountAsync(s => s.Status == ChatSessionStatus.Queued && s.StartedAtUtc < startedAtUtc, ct);

    public Task AddAsync(ChatSession session, CancellationToken ct)
    {
        dbContext.ChatSessions.Add(session);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid chatSessionId, CancellationToken ct) =>
        await dbContext.ChatMessages.Where(m => m.ChatSessionId == chatSessionId).ToListAsync(ct);

    public Task AddMessageAsync(ChatMessage message, CancellationToken ct)
    {
        dbContext.ChatMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
