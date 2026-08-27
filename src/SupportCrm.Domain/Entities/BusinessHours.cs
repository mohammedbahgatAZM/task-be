namespace SupportCrm.Domain.Entities;

// One row per day of week, seeded for all 7 (see SupportCrmDbContext). A day with
// IsWorkingDay=false is skipped entirely by BusinessCalendarService regardless of
// StartTime/EndTime. Keyed by DayOfWeek — one global calendar, not per-team/per-region.
public class BusinessHours
{
    public DayOfWeek DayOfWeek { get; private set; }
    public bool IsWorkingDay { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }

    private BusinessHours() { } // EF Core

    public BusinessHours(DayOfWeek dayOfWeek, bool isWorkingDay, TimeOnly startTime, TimeOnly endTime)
    {
        if (isWorkingDay && startTime >= endTime)
            throw new ArgumentException("Start time must be before end time on a working day.", nameof(startTime));
        DayOfWeek = dayOfWeek;
        IsWorkingDay = isWorkingDay;
        StartTime = startTime;
        EndTime = endTime;
    }

    public void Update(bool isWorkingDay, TimeOnly startTime, TimeOnly endTime)
    {
        if (isWorkingDay && startTime >= endTime)
            throw new ArgumentException("Start time must be before end time on a working day.", nameof(startTime));
        IsWorkingDay = isWorkingDay;
        StartTime = startTime;
        EndTime = endTime;
    }
}
