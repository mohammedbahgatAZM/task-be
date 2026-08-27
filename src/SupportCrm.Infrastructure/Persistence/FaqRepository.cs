namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class FaqRepository(SupportCrmDbContext dbContext) : IFaqRepository
{
    public Task<Faq?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.Faqs.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<Faq>> GetAllAsync(CancellationToken ct) =>
        await dbContext.Faqs.ToListAsync(ct);

    public async Task<IReadOnlyList<Faq>> GetByCategoryAsync(Guid kbCategoryId, CancellationToken ct) =>
        await dbContext.Faqs.Where(f => f.KbCategoryId == kbCategoryId).ToListAsync(ct);

    public async Task<IReadOnlyList<Faq>> GetMostUnhelpfulAsync(int take, CancellationToken ct) =>
        await dbContext.Faqs
            .Where(f => f.NotHelpfulCount > 0)
            .OrderByDescending(f => f.NotHelpfulCount)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Faq>> SearchAsync(string query, CancellationToken ct) =>
        await dbContext.Faqs
            .Where(f =>
                EF.Functions.ILike(f.QuestionEn ?? "", $"%{query}%") || EF.Functions.ILike(f.QuestionAr ?? "", $"%{query}%") ||
                EF.Functions.ILike(f.AnswerEn ?? "", $"%{query}%") || EF.Functions.ILike(f.AnswerAr ?? "", $"%{query}%"))
            .ToListAsync(ct);

    public Task AddAsync(Faq faq, CancellationToken ct)
    {
        dbContext.Faqs.Add(faq);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
