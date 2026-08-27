namespace SupportCrm.Domain.Entities;

public class Faq
{
    public Guid Id { get; private set; }
    public Guid? KbCategoryId { get; private set; }
    public string? QuestionEn { get; private set; }
    public string? QuestionAr { get; private set; }
    public string? AnswerEn { get; private set; }
    public string? AnswerAr { get; private set; }
    public int HelpfulCount { get; private set; }
    public int NotHelpfulCount { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Faq() { } // EF Core

    public Faq(Guid? kbCategoryId, string? questionEn, string? questionAr, string? answerEn, string? answerAr, DateTimeOffset createdAtUtc)
    {
        var hasEnglish = !string.IsNullOrWhiteSpace(questionEn) && !string.IsNullOrWhiteSpace(answerEn);
        var hasArabic = !string.IsNullOrWhiteSpace(questionAr) && !string.IsNullOrWhiteSpace(answerAr);
        if (!hasEnglish && !hasArabic)
            throw new ArgumentException("A question+answer pair is required in at least one language.", nameof(questionEn));

        Id = Guid.NewGuid();
        KbCategoryId = kbCategoryId;
        QuestionEn = questionEn;
        QuestionAr = questionAr;
        AnswerEn = answerEn;
        AnswerAr = answerAr;
        CreatedAtUtc = createdAtUtc;
    }

    public void MarkHelpful() => HelpfulCount++;
    public void MarkNotHelpful() => NotHelpfulCount++;
}
