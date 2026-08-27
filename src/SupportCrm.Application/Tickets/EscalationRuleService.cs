namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class EscalationRuleService(IEscalationRuleRepository repository)
{
    public async Task<EscalationRuleDto> CreateAsync(CreateEscalationRuleRequest request, CancellationToken ct)
    {
        var rule = new EscalationRule(request.Name.Trim(), request.SortOrder, request.CategoryId, request.Priority);
        await repository.AddAsync(rule, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task<IReadOnlyList<EscalationRuleDto>> GetActiveOrderedAsync(CancellationToken ct) =>
        (await repository.GetActiveOrderedAsync(ct)).Select(ToDto).ToList();

    public async Task<EscalationTierDto> AddTierAsync(Guid escalationRuleId, CreateEscalationTierRequest request, CancellationToken ct)
    {
        var tier = new EscalationTier(escalationRuleId, request.TierNumber, request.TriggerPercentage, request.ReassignToAgentId, request.ReassignToTeamId, request.RaisePriorityTo, request.NotifySupervisor);
        await repository.AddTierAsync(tier, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(tier);
    }

    public async Task<IReadOnlyList<EscalationTierDto>> GetTiersAsync(Guid escalationRuleId, CancellationToken ct) =>
        (await repository.GetTiersAsync(escalationRuleId, ct)).OrderBy(t => t.TierNumber).Select(ToDto).ToList();

    public async Task<IReadOnlyList<EscalationLogEntryDto>> GetLogForTicketAsync(Guid ticketId, CancellationToken ct) =>
        (await repository.GetLogForTicketAsync(ticketId, ct))
            .OrderByDescending(e => e.TriggeredAtUtc)
            .Select(e => new EscalationLogEntryDto(e.Id, e.EscalationRuleId, e.TierNumber, e.ActionSummary, e.TriggeredAtUtc))
            .ToList();

    private static EscalationRuleDto ToDto(EscalationRule r) => new(r.Id, r.Name, r.SortOrder, r.CategoryId, r.Priority);
    private static EscalationTierDto ToDto(EscalationTier t) => new(t.Id, t.TierNumber, t.TriggerPercentage, t.ReassignToAgentId, t.ReassignToTeamId, t.RaisePriorityTo, t.NotifySupervisor);
}
