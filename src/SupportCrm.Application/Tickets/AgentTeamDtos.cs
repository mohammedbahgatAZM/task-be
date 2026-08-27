namespace SupportCrm.Application.Tickets;

public record CreateAgentRequest(string Name);
public record AgentDto(
    Guid Id, string Name, bool IsAvailable, bool CanViewSensitiveData, bool IsSupervisor, bool IsKnowledgeBaseEditor,
    string PreferredLanguage = "en", Guid? DepartmentId = null, Guid? BranchId = null);
public record SetAgentAvailabilityRequest(bool IsAvailable);
public record SetAgentSensitiveDataAccessRequest(bool CanViewSensitiveData);
public record SetAgentSupervisorRequest(bool IsSupervisor);
public record SetAgentKnowledgeBaseEditorRequest(bool IsKnowledgeBaseEditor);
public record SetAgentLanguageRequest(string Language);
public record CreateTeamRequest(string Name);
public record TeamDto(Guid Id, string Name, Guid? DepartmentId = null);
public record AgentLoadDto(Guid AgentId, string AgentName, int OpenTicketCount);
public record AssignTicketRequest(Guid? AgentId, Guid? TeamId, string ChangedBy);
