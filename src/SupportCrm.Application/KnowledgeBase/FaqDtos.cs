namespace SupportCrm.Application.KnowledgeBase;

public record CreateFaqRequest(Guid? KbCategoryId, string? QuestionEn, string? QuestionAr, string? AnswerEn, string? AnswerAr);
public record FaqDto(Guid Id, Guid? KbCategoryId, string? QuestionEn, string? QuestionAr, string? AnswerEn, string? AnswerAr, int HelpfulCount, int NotHelpfulCount);
