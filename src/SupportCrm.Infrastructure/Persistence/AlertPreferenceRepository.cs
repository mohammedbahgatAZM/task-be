namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AlertPreferenceRepository(SupportCrmDbContext dbContext) : IAlertPreferenceRepository
{
    public Task<AlertPreference?> GetByAgentIdAsync(Guid agentId, CancellationToken ct) =>
        dbContext.AlertPreferences.FirstOrDefaultAsync(p => p.AgentId == agentId, ct);

    public async Task<IReadOnlyList<AlertPreference>> GetWithDigestEnabledAsync(CancellationToken ct) =>
        await dbContext.AlertPreferences.Where(p => p.DigestFrequency != DigestFrequency.None).ToListAsync(ct);

    public Task UpsertAsync(AlertPreference preference, CancellationToken ct)
    {
        if (dbContext.Entry(preference).State == EntityState.Detached)
            dbContext.AlertPreferences.Add(preference);
        return Task.CompletedTask;
    }

    public Task<bool> HasAlertBeenSentAsync(Guid ticketId, string kind, CancellationToken ct) =>
        dbContext.SlaAlertLogs.AnyAsync(e => e.TicketId == ticketId && e.Kind == kind, ct);

    public Task AddAlertLogAsync(SlaAlertLog entry, CancellationToken ct)
    {
        dbContext.SlaAlertLogs.Add(entry);
        return Task.CompletedTask;
    }

    public async Task<DateTimeOffset?> GetLastDigestSentAsync(Guid agentId, CancellationToken ct) =>
        await dbContext.DigestLogEntries.Where(e => e.AgentId == agentId).OrderByDescending(e => e.SentAtUtc).Select(e => (DateTimeOffset?)e.SentAtUtc).FirstOrDefaultAsync(ct);

    public Task AddDigestLogAsync(DigestLogEntry entry, CancellationToken ct)
    {
        dbContext.DigestLogEntries.Add(entry);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
