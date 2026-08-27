namespace SupportCrm.Application.Sla;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class SlaTargetService(ISlaTargetRepository repository)
{
    public async Task<SlaTargetDto> CreateAsync(CreateSlaTargetRequest request, CancellationToken ct)
    {
        var target = new SlaTarget(request.Name.Trim(), request.Priority, request.CategoryId, request.Tier, request.ResponseTargetMinutes, request.ResolutionTargetMinutes);
        await repository.AddAsync(target, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(target);
    }

    public async Task<IReadOnlyList<SlaTargetDto>> GetActiveAsync(CancellationToken ct) =>
        (await repository.GetActiveAsync(ct)).Select(ToDto).ToList();

    // Priority is mandatory and matched exactly; Category/Tier only narrow. Among all matches,
    // the most specific (Specificity()) wins — see SlaTarget's doc comment.
    public async Task<SlaTarget?> ResolveAsync(TicketPriority priority, Guid? categoryId, CustomerTier tier, CancellationToken ct) =>
        (await repository.GetActiveAsync(ct))
            .Where(t => t.Priority == priority)
            .Where(t => t.CategoryId is null || t.CategoryId == categoryId)
            .Where(t => t.Tier is null || t.Tier == tier)
            .OrderByDescending(t => t.Specificity())
            .FirstOrDefault();

    private static SlaTargetDto ToDto(SlaTarget t) => new(t.Id, t.Name, t.Priority, t.CategoryId, t.Tier, t.ResponseTargetMinutes, t.ResolutionTargetMinutes);
}
