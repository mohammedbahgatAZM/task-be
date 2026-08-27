namespace SupportCrm.Application.KnowledgeBase;

public record CreateKbCategoryRequest(string? NameEn, string? NameAr);
public record KbCategoryDto(Guid Id, string? NameEn, string? NameAr);
