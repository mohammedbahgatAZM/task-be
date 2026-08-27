namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AssignmentRuleService(IAssignmentRuleRepository repository)
{
    public async Task<AssignmentRuleDto> CreateAsync(CreateAssignmentRuleRequest request, CancellationToken ct)
    {
        var rule = new AssignmentRule(request.Name.Trim(), request.SortOrder, request.CategoryId, request.Channel, request.Language, request.RequiredSkill, request.TargetTeamId);
        await repository.AddAsync(rule, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task<IReadOnlyList<AssignmentRuleDto>> GetActiveOrderedAsync(CancellationToken ct) =>
        (await repository.GetActiveOrderedAsync(ct)).Select(ToDto).ToList();

    private static AssignmentRuleDto ToDto(AssignmentRule r) => new(r.Id, r.Name, r.SortOrder, r.CategoryId, r.Channel, r.Language, r.RequiredSkill, r.TargetTeamId);
}
