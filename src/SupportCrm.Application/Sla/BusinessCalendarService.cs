namespace SupportCrm.Application.Sla;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

// Treats every DateTimeOffset as UTC-aligned to calendar days (no timezone conversion) — one
// global calendar, per the story's explicit UTC-only simplification.
public class BusinessCalendarService(IBusinessCalendarRepository calendarRepository)
{
    public async Task<DateTimeOffset> AddBusinessMinutesAsync(DateTimeOffset startUtc, int minutes, CancellationToken ct)
    {
        var (hoursByDay, holidays) = await LoadAsync(ct);
        var cursor = startUtc;
        var remaining = minutes;
        while (remaining > 0)
        {
            if (!IsWorkingInstant(cursor, hoursByDay, holidays, out var windowStart, out var windowEnd))
            {
                cursor = NextDayStart(cursor);
                continue;
            }
            if (cursor < windowStart) cursor = windowStart;

            var availableToday = (int)(windowEnd - cursor).TotalMinutes;
            if (remaining <= availableToday) return cursor.AddMinutes(remaining);
            remaining -= availableToday;
            cursor = NextDayStart(cursor);
        }
        return cursor;
    }

    public async Task<int> CalculateBusinessMinutesBetweenAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct)
    {
        if (endUtc <= startUtc) return 0;
        var (hoursByDay, holidays) = await LoadAsync(ct);
        var total = 0;
        var cursor = startUtc;
        while (cursor < endUtc)
        {
            var dayEnd = NextDayStart(cursor);
            var segmentEnd = dayEnd < endUtc ? dayEnd : endUtc;
            if (IsWorkingDay(cursor, hoursByDay, holidays, out var day))
            {
                var windowStart = AtTime(cursor, day!.StartTime);
                var windowEnd = AtTime(cursor, day.EndTime);
                var from = cursor > windowStart ? cursor : windowStart;
                var to = segmentEnd < windowEnd ? segmentEnd : windowEnd;
                if (to > from) total += (int)(to - from).TotalMinutes;
            }
            cursor = dayEnd;
        }
        return total;
    }

    private static bool IsWorkingDay(DateTimeOffset dt, IReadOnlyDictionary<DayOfWeek, BusinessHours> hoursByDay, HashSet<DateOnly> holidays, out BusinessHours? day)
    {
        day = hoursByDay.GetValueOrDefault(dt.DayOfWeek);
        return day is { IsWorkingDay: true } && !holidays.Contains(DateOnly.FromDateTime(dt.UtcDateTime));
    }

    private static bool IsWorkingInstant(DateTimeOffset dt, IReadOnlyDictionary<DayOfWeek, BusinessHours> hoursByDay, HashSet<DateOnly> holidays, out DateTimeOffset windowStart, out DateTimeOffset windowEnd)
    {
        windowStart = windowEnd = default;
        if (!IsWorkingDay(dt, hoursByDay, holidays, out var day)) return false;
        windowStart = AtTime(dt, day!.StartTime);
        windowEnd = AtTime(dt, day.EndTime);
        return dt < windowEnd; // still time left today; caller clamps dt up to windowStart if early
    }

    private static DateTimeOffset NextDayStart(DateTimeOffset dt) => new(dt.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
    private static DateTimeOffset AtTime(DateTimeOffset dt, TimeOnly time) => new DateTimeOffset(dt.UtcDateTime.Date, TimeSpan.Zero).Add(time.ToTimeSpan());

    private async Task<(IReadOnlyDictionary<DayOfWeek, BusinessHours> hoursByDay, HashSet<DateOnly> holidays)> LoadAsync(CancellationToken ct)
    {
        var hours = await calendarRepository.GetBusinessHoursAsync(ct);
        var holidays = await calendarRepository.GetHolidaysAsync(ct);
        return (hours.ToDictionary(h => h.DayOfWeek), holidays.Select(h => h.Date).ToHashSet());
    }
}
