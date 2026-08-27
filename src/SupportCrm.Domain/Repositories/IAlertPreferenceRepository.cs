namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IAlertPreferenceRepository
{
    Task<AlertPreference?> GetByAgentIdAsync(Guid agentId, CancellationToken ct);
    Task<IReadOnlyList<AlertPreference>> GetWithDigestEnabledAsync(CancellationToken ct);
    Task UpsertAsync(AlertPreference preference, CancellationToken ct);
    Task<bool> HasAlertBeenSentAsync(Guid ticketId, string kind, CancellationToken ct);
    Task AddAlertLogAsync(SlaAlertLog entry, CancellationToken ct);
    Task<DateTimeOffset?> GetLastDigestSentAsync(Guid agentId, CancellationToken ct);
    Task AddDigestLogAsync(DigestLogEntry entry, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
