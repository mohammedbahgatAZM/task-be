namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketCategoryService(ITicketCategoryRepository repository)
{
    public async Task<TicketCategoryDto> CreateAsync(CreateTicketCategoryRequest request, CancellationToken ct)
    {
        var category = new TicketCategory(request.Name.Trim(), request.ParentCategoryId);
        await repository.AddAsync(category, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task<IReadOnlyList<TicketCategoryDto>> GetActiveAsync(CancellationToken ct) =>
        (await repository.GetActiveAsync(ct)).Select(ToDto).ToList();

    public async Task SetDepartmentAsync(Guid categoryId, Guid? departmentId, CancellationToken ct)
    {
        var category = await repository.GetByIdAsync(categoryId, ct) ?? throw new KeyNotFoundException($"Category '{categoryId}' was not found.");
        category.SetDepartment(departmentId);
        await repository.SaveChangesAsync(ct);
    }

    private static TicketCategoryDto ToDto(TicketCategory c) => new(c.Id, c.Name, c.ParentCategoryId, c.DepartmentId);
}
