namespace SupportCrm.Application.Platform;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class BranchService(IBranchRepository repository)
{
    public async Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken ct)
    {
        var branch = new Branch(request.Name, request.Code);
        await repository.AddAsync(branch, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(branch);
    }

    public async Task<IReadOnlyList<BranchDto>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(ToDto).ToList();

    public async Task UpdateSettingsAsync(Guid id, UpdateBranchSettingsRequest request, CancellationToken ct)
    {
        var branch = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Branch '{id}' was not found.");
        branch.UpdateSettings(request.DefaultLanguage, request.ContactNumber);
        await repository.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct)
    {
        var branch = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Branch '{id}' was not found.");
        if (isActive) branch.Activate(); else branch.Deactivate();
        await repository.SaveChangesAsync(ct);
    }

    private static BranchDto ToDto(Branch b) => new(b.Id, b.Name, b.Code, b.DefaultLanguage, b.ContactNumber, b.IsActive);
}
