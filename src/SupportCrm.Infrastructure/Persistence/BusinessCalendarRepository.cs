namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class BusinessCalendarRepository(SupportCrmDbContext dbContext) : IBusinessCalendarRepository
{
    public async Task<IReadOnlyList<BusinessHours>> GetBusinessHoursAsync(CancellationToken ct) =>
        await dbContext.BusinessHours.ToListAsync(ct);

    public Task<BusinessHours?> GetBusinessHoursForDayAsync(DayOfWeek day, CancellationToken ct) =>
        dbContext.BusinessHours.FirstOrDefaultAsync(h => h.DayOfWeek == day, ct);

    public async Task UpdateBusinessHoursAsync(DayOfWeek day, bool isWorkingDay, TimeOnly startTime, TimeOnly endTime, CancellationToken ct)
    {
        var hours = await dbContext.BusinessHours.FirstOrDefaultAsync(h => h.DayOfWeek == day, ct)
            ?? throw new KeyNotFoundException($"Business hours for '{day}' were not found.");
        hours.Update(isWorkingDay, startTime, endTime);
    }

    public async Task<IReadOnlyList<Holiday>> GetHolidaysAsync(CancellationToken ct) =>
        await dbContext.Holidays.ToListAsync(ct);

    public Task AddHolidayAsync(Holiday holiday, CancellationToken ct)
    {
        dbContext.Holidays.Add(holiday);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
