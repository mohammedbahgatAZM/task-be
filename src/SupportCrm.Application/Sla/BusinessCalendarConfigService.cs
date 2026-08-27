namespace SupportCrm.Application.Sla;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class BusinessCalendarConfigService(IBusinessCalendarRepository repository)
{
    public async Task<IReadOnlyList<BusinessHoursDto>> GetBusinessHoursAsync(CancellationToken ct) =>
        (await repository.GetBusinessHoursAsync(ct)).Select(h => new BusinessHoursDto(h.DayOfWeek, h.IsWorkingDay, h.StartTime, h.EndTime)).ToList();

    public Task SetBusinessHoursAsync(SetBusinessHoursRequest request, CancellationToken ct) =>
        repository.UpdateBusinessHoursAsync(request.DayOfWeek, request.IsWorkingDay, request.StartTime, request.EndTime, ct);

    public async Task<HolidayDto> AddHolidayAsync(CreateHolidayRequest request, CancellationToken ct)
    {
        var holiday = new Holiday(request.Date, request.Name.Trim());
        await repository.AddHolidayAsync(holiday, ct);
        await repository.SaveChangesAsync(ct);
        return new HolidayDto(holiday.Id, holiday.Date, holiday.Name);
    }

    public async Task<IReadOnlyList<HolidayDto>> GetHolidaysAsync(CancellationToken ct) =>
        (await repository.GetHolidaysAsync(ct)).Select(h => new HolidayDto(h.Id, h.Date, h.Name)).ToList();
}
