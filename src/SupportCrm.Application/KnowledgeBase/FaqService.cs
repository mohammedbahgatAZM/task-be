namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class FaqService(IFaqRepository repository, TimeProvider timeProvider)
{
    public async Task<FaqDto> CreateAsync(CreateFaqRequest request, CancellationToken ct)
    {
        var faq = new Faq(request.KbCategoryId, request.QuestionEn?.Trim(), request.QuestionAr?.Trim(),
            request.AnswerEn?.Trim(), request.AnswerAr?.Trim(), timeProvider.GetUtcNow());
        await repository.AddAsync(faq, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(faq);
    }

    public async Task<IReadOnlyList<FaqDto>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<FaqDto>> GetByCategoryAsync(Guid kbCategoryId, CancellationToken ct) =>
        (await repository.GetByCategoryAsync(kbCategoryId, ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<FaqDto>> GetMostUnhelpfulAsync(int take, CancellationToken ct) =>
        (await repository.GetMostUnhelpfulAsync(take, ct)).Select(ToDto).ToList();

    public async Task MarkHelpfulAsync(Guid id, CancellationToken ct)
    {
        var faq = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"FAQ '{id}' was not found.");
        faq.MarkHelpful();
        await repository.SaveChangesAsync(ct);
    }

    public async Task MarkNotHelpfulAsync(Guid id, CancellationToken ct)
    {
        var faq = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"FAQ '{id}' was not found.");
        faq.MarkNotHelpful();
        await repository.SaveChangesAsync(ct);
    }

    internal static FaqDto ToDto(Faq f) => new(f.Id, f.KbCategoryId, f.QuestionEn, f.QuestionAr, f.AnswerEn, f.AnswerAr, f.HelpfulCount, f.NotHelpfulCount);
}
