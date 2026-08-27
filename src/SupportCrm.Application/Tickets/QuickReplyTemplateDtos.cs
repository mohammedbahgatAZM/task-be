namespace SupportCrm.Application.Tickets;

public record CreateQuickReplyTemplateRequest(string Category, string Name, string Body, string CreatedBy);
public record UpdateQuickReplyTemplateRequest(string Category, string Name, string Body);
public record QuickReplyTemplateDto(Guid Id, string Category, string Name, string Body, bool IsRetired, DateTimeOffset CreatedAtUtc);
public record RenderQuickReplyTemplateRequest(Guid TicketId);
public record RenderedQuickReplyDto(string Body);
