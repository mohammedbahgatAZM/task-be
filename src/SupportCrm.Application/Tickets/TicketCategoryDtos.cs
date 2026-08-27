namespace SupportCrm.Application.Tickets;

public record CreateTicketCategoryRequest(string Name, Guid? ParentCategoryId);
public record TicketCategoryDto(Guid Id, string Name, Guid? ParentCategoryId, Guid? DepartmentId = null);
