namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class KbCategoryService(IKbCategoryRepository repository)
{
    public async Task<KbCategoryDto> CreateAsync(CreateKbCategoryRequest request, CancellationToken ct)
    {
        var category = new KbCategory(request.NameEn?.Trim(), request.NameAr?.Trim());
        await repository.AddAsync(category, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task<IReadOnlyList<KbCategoryDto>> GetActiveAsync(CancellationToken ct) =>
        (await repository.GetActiveAsync(ct)).Select(ToDto).ToList();

    private static KbCategoryDto ToDto(KbCategory c) => new(c.Id, c.NameEn, c.NameAr);
}
