namespace SupportCrm.Application.Security;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Reports;

public class AuditLogService(IAuditLogRepository repository, IReportExporter exporter, TimeProvider timeProvider)
{
    public async Task LogAsync(Guid? userId, string userEmail, string httpMethod, string path, string actionSummary, string? ipAddress, CancellationToken ct)
    {
        var entry = new AuditLogEntry(userId, userEmail, httpMethod, path, actionSummary, ipAddress, timeProvider.GetUtcNow());
        await repository.AddAsync(entry, ct);
        await repository.SaveChangesAsync(ct);
    }

    // "Action type" is the HTTP method (POST/PUT/DELETE/PATCH) — this codebase has no richer
    // action taxonomy, and the method itself already distinguishes creates/edits/deletes cleanly.
    public async Task<IReadOnlyList<AuditLogEntryDto>> GetLogsAsync(AuditLogQuery query, CancellationToken ct)
    {
        var all = await repository.GetAllAsync(ct);
        IEnumerable<AuditLogEntry> filtered = all;
        if (query.UserId is not null) filtered = filtered.Where(e => e.UserId == query.UserId);
        if (query.From is not null) filtered = filtered.Where(e => e.OccurredAtUtc >= query.From);
        if (query.To is not null) filtered = filtered.Where(e => e.OccurredAtUtc <= query.To);
        if (!string.IsNullOrWhiteSpace(query.ActionType)) filtered = filtered.Where(e => e.HttpMethod.Equals(query.ActionType, StringComparison.OrdinalIgnoreCase));
        return filtered.OrderByDescending(e => e.OccurredAtUtc).Select(ToDto).ToList();
    }

    public async Task<byte[]> ExportAsync(AuditLogQuery query, ReportExportFormat format, CancellationToken ct)
    {
        var logs = await GetLogsAsync(query, ct);
        var columns = new[] { "Timestamp (UTC)", "User", "Method", "Path", "Summary", "IP" };
        var rows = logs.Select(l => (IReadOnlyList<string>)new[] { l.OccurredAtUtc.ToString("u"), l.UserEmail, l.HttpMethod, l.Path, l.ActionSummary, l.IpAddress ?? "" }).ToList();
        var data = new ReportExportData("Audit log", columns, rows);
        return format == ReportExportFormat.Xlsx ? exporter.ExportToExcel(data) : exporter.ExportToPdf(data);
    }

    private static AuditLogEntryDto ToDto(AuditLogEntry e) => new(e.Id, e.UserId, e.UserEmail, e.HttpMethod, e.Path, e.ActionSummary, e.IpAddress, e.OccurredAtUtc);
}
