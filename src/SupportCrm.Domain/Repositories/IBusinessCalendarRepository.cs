namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IBusinessCalendarRepository
{
    Task<IReadOnlyList<BusinessHours>> GetBusinessHoursAsync(CancellationToken ct);
    Task<BusinessHours?> GetBusinessHoursForDayAsync(DayOfWeek day, CancellationToken ct);
    Task UpdateBusinessHoursAsync(DayOfWeek day, bool isWorkingDay, TimeOnly startTime, TimeOnly endTime, CancellationToken ct);
    Task<IReadOnlyList<Holiday>> GetHolidaysAsync(CancellationToken ct);
    Task AddHolidayAsync(Holiday holiday, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
