namespace SupportCrm.Application.Reports;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketReportService(
    ITicketRepository ticketRepository, ICustomerRepository customerRepository, ITicketCategoryRepository categoryRepository,
    IDepartmentRepository departmentRepository, IReportExporter exporter)
{
    public async Task<TicketVolumeReportDto> GetVolumeReportAsync(TicketReportQuery query, CancellationToken ct)
    {
        var customersById = (await customerRepository.GetAllAsync(ct)).ToDictionary(c => c.Id);
        var tickets = Filter(await ticketRepository.GetAllAsync(ct), query, customersById);

        var categoriesById = (await categoryRepository.GetAllAsync(ct)).ToDictionary(c => c.Id, c => c.Name);
        var departmentsById = (await departmentRepository.GetAllAsync(ct)).ToDictionary(d => d.Id, d => d.Name);

        var byChannel = tickets.GroupBy(t => t.Channel.ToString()).ToDictionary(g => g.Key, g => g.Count());
        var byCategory = tickets
            .GroupBy(t => t.CategoryId is Guid categoryId && categoriesById.TryGetValue(categoryId, out var name) ? name : "Uncategorized")
            .ToDictionary(g => g.Key, g => g.Count());
        var byBranch = tickets
            .GroupBy(t => customersById.TryGetValue(t.CustomerId, out var c) ? (c.Branch ?? "Unspecified") : "Unspecified")
            .ToDictionary(g => g.Key, g => g.Count());
        var byDepartment = tickets
            .GroupBy(t => t.DepartmentId is Guid departmentId && departmentsById.TryGetValue(departmentId, out var name) ? name : "Unassigned")
            .ToDictionary(g => g.Key, g => g.Count());

        var trend = tickets
            .GroupBy(t => BucketStart(t.CreatedAtUtc, query.Granularity))
            .OrderBy(g => g.Key)
            .Select(g => new VolumeTrendPointDto(g.Key, g.Count()))
            .ToList();

        return new TicketVolumeReportDto(tickets.Count, trend, byChannel, byCategory, byBranch, byDepartment);
    }

    public async Task<byte[]> ExportVolumeReportAsync(TicketReportQuery query, ReportExportFormat format, CancellationToken ct)
    {
        var customersById = (await customerRepository.GetAllAsync(ct)).ToDictionary(c => c.Id);
        var tickets = Filter(await ticketRepository.GetAllAsync(ct), query, customersById);
        var categoriesById = (await categoryRepository.GetAllAsync(ct)).ToDictionary(c => c.Id, c => c.Name);
        var departmentsById = (await departmentRepository.GetAllAsync(ct)).ToDictionary(d => d.Id, d => d.Name);

        var columns = new[] { "Reference", "Subject", "Channel", "Status", "Priority", "Category", "Branch", "Department", "Created (UTC)" };
        var rows = tickets
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => (IReadOnlyList<string>)new[]
            {
                t.ReferenceNumber, t.Subject, t.Channel.ToString(), t.Status.ToString(), t.Priority.ToString(),
                t.CategoryId is Guid categoryId && categoriesById.TryGetValue(categoryId, out var name) ? name : "Uncategorized",
                customersById.TryGetValue(t.CustomerId, out var c) ? (c.Branch ?? "Unspecified") : "Unspecified",
                t.DepartmentId is Guid departmentId && departmentsById.TryGetValue(departmentId, out var deptName) ? deptName : "Unassigned",
                t.CreatedAtUtc.ToString("u")
            })
            .ToList();

        var data = new ReportExportData("Ticket report", columns, rows);
        return format == ReportExportFormat.Xlsx ? exporter.ExportToExcel(data) : exporter.ExportToPdf(data);
    }

    private static List<Ticket> Filter(IReadOnlyList<Ticket> all, TicketReportQuery query, IReadOnlyDictionary<Guid, Customer> customersById)
    {
        IEnumerable<Ticket> filtered = all;
        if (query.From is not null) filtered = filtered.Where(t => t.CreatedAtUtc >= query.From);
        if (query.To is not null) filtered = filtered.Where(t => t.CreatedAtUtc <= query.To);
        if (query.Channel is not null) filtered = filtered.Where(t => t.Channel == query.Channel);
        if (query.CategoryId is not null) filtered = filtered.Where(t => t.CategoryId == query.CategoryId);
        if (query.DepartmentId is not null) filtered = filtered.Where(t => t.DepartmentId == query.DepartmentId);
        if (!string.IsNullOrWhiteSpace(query.Branch))
            filtered = filtered.Where(t => customersById.TryGetValue(t.CustomerId, out var c) && string.Equals(c.Branch, query.Branch, StringComparison.OrdinalIgnoreCase));
        return filtered.ToList();
    }

    // Monday-start week, per this codebase's existing calendar convention. Public + static so
    // RM-2's weekly trend and RM-4's CSAT trend can both reuse it verbatim.
    public static DateOnly BucketStart(DateTimeOffset dt, ReportGranularity granularity)
    {
        var date = DateOnly.FromDateTime(dt.UtcDateTime);
        return granularity switch
        {
            ReportGranularity.Daily => date,
            ReportGranularity.Weekly => date.AddDays(-((7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7)),
            ReportGranularity.Monthly => new DateOnly(date.Year, date.Month, 1),
            _ => date
        };
    }
}
