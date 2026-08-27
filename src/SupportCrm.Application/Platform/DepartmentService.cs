namespace SupportCrm.Application.Platform;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class DepartmentService(IDepartmentRepository repository)
{
    public async Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request, CancellationToken ct)
    {
        var department = new Department(request.Name);
        await repository.AddAsync(department, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(department);
    }

    public async Task<IReadOnlyList<DepartmentDto>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(ToDto).ToList();

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct)
    {
        var department = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Department '{id}' was not found.");
        if (isActive) department.Activate(); else department.Deactivate();
        await repository.SaveChangesAsync(ct);
    }

    public async Task SetDefaultChannelAsync(Guid id, SetDepartmentChannelRequest request, CancellationToken ct)
    {
        var department = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Department '{id}' was not found.");
        department.SetDefaultChannel(request.Channel);
        await repository.SaveChangesAsync(ct);
    }

    private static DepartmentDto ToDto(Department d) => new(d.Id, d.Name, d.IsActive, d.DefaultForChannel);
}
