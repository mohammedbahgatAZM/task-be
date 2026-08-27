namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

public record AlertPreferenceDto(Guid AgentId, bool EmailEnabled, bool PushEnabled, int WarningThresholdPercentage, DigestFrequency DigestFrequency);
public record SetAlertPreferenceRequest(bool EmailEnabled, bool PushEnabled, int WarningThresholdPercentage, DigestFrequency DigestFrequency);
public record AtRiskTicketDto(Guid TicketId, string ReferenceNumber, TicketPriority Priority, int ResolutionRemainingMinutes, bool IsBreached, string DeepLinkPath);
