namespace SupportCrm.Application.Platform;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

// Two-step resolution, category first: an explicit category-to-department assignment always
// beats a channel default. Neither matching leaves the ticket unrouted (null), same "no match
// is a valid outcome" convention SlaTargetService.ResolveAsync already established.
public class TicketDepartmentRoutingService(ITicketCategoryRepository categoryRepository, IDepartmentRepository departmentRepository)
{
    public async Task<Guid?> ResolveDepartmentAsync(Guid? categoryId, TicketChannel channel, CancellationToken ct)
    {
        if (categoryId is Guid catId)
        {
            var category = await categoryRepository.GetByIdAsync(catId, ct);
            if (category?.DepartmentId is Guid deptId) return deptId;
        }

        var departments = await departmentRepository.GetAllAsync(ct);
        return departments.FirstOrDefault(d => d.IsActive && d.DefaultForChannel == channel)?.Id;
    }
}
