namespace SupportCrm.Application.Sla;

using SupportCrm.Domain.Entities;

public record CreateSlaTargetRequest(string Name, TicketPriority Priority, Guid? CategoryId, CustomerTier? Tier, int ResponseTargetMinutes, int ResolutionTargetMinutes);
public record SlaTargetDto(Guid Id, string Name, TicketPriority Priority, Guid? CategoryId, CustomerTier? Tier, int ResponseTargetMinutes, int ResolutionTargetMinutes);
public record SetBusinessHoursRequest(DayOfWeek DayOfWeek, bool IsWorkingDay, TimeOnly StartTime, TimeOnly EndTime);
public record BusinessHoursDto(DayOfWeek DayOfWeek, bool IsWorkingDay, TimeOnly StartTime, TimeOnly EndTime);
public record CreateHolidayRequest(DateOnly Date, string Name);
public record HolidayDto(Guid Id, DateOnly Date, string Name);
public record TicketSlaStatusDto(
    Guid TicketId, Guid SlaTargetId, int ResolutionTargetMinutes,
    DateTimeOffset ResponseDueAtUtc, DateTimeOffset ResolutionDueAtUtc,
    bool IsResponseBreached, bool IsResolutionBreached,
    int ResponseRemainingMinutes, int ResolutionRemainingMinutes);
