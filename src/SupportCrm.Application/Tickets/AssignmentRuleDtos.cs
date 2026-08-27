namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

public record CreateAssignmentRuleRequest(string Name, int SortOrder, Guid? CategoryId, TicketChannel? Channel, string? Language, string? RequiredSkill, Guid? TargetTeamId);
public record AssignmentRuleDto(Guid Id, string Name, int SortOrder, Guid? CategoryId, TicketChannel? Channel, string? Language, string? RequiredSkill, Guid? TargetTeamId);
public record AddAgentSkillRequest(string Skill);
public record AddAgentLanguageRequest(string Language);
