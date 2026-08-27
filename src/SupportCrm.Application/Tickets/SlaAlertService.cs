namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Sla;

public class SlaAlertService(
    ITicketRepository ticketRepository,
    IAlertPreferenceRepository preferenceRepository,
    SlaCalculationService slaCalculationService,
    AgentNotificationService notificationService,
    ISlaAlertNotifier alertNotifier,
    TimeProvider timeProvider)
{
    // Fixed floor for the manager-facing "at risk" view, independent of any one agent's own
    // warning threshold — a ticket qualifies once it's 80%+ through its resolution window or
    // already breached, regardless of who (if anyone) it's assigned to.
    private const int AtRiskFloorPercentage = 80;

    public async Task<AlertPreferenceDto> SetPreferenceAsync(Guid agentId, SetAlertPreferenceRequest request, CancellationToken ct)
    {
        var preference = await preferenceRepository.GetByAgentIdAsync(agentId, ct) ?? new AlertPreference(agentId);
        preference.Update(request.EmailEnabled, request.PushEnabled, request.WarningThresholdPercentage, request.DigestFrequency);
        await preferenceRepository.UpsertAsync(preference, ct);
        await preferenceRepository.SaveChangesAsync(ct);
        return ToDto(preference);
    }

    public async Task<AlertPreferenceDto> GetPreferenceAsync(Guid agentId, CancellationToken ct)
    {
        var preference = await preferenceRepository.GetByAgentIdAsync(agentId, ct) ?? new AlertPreference(agentId); // unsaved defaults until the agent's first Set
        return ToDto(preference);
    }

    public async Task<IReadOnlyList<AtRiskTicketDto>> GetAtRiskTicketsAsync(CancellationToken ct)
    {
        var openTickets = await ticketRepository.GetOpenAsync(ct);
        var result = new List<AtRiskTicketDto>();
        foreach (var ticket in openTickets)
        {
            var status = await slaCalculationService.GetStatusAsync(ticket.Id, ct);
            if (status is null) continue;
            var elapsedPercentage = 100 - status.ResolutionRemainingMinutes * 100 / Math.Max(1, status.ResolutionTargetMinutes);
            if (status.IsResolutionBreached || elapsedPercentage >= AtRiskFloorPercentage)
                result.Add(new AtRiskTicketDto(ticket.Id, ticket.ReferenceNumber, ticket.Priority, status.ResolutionRemainingMinutes, status.IsResolutionBreached, $"/tickets/{ticket.Id}"));
        }
        return result;
    }

    // Called every SlaEscalationHostedService tick. Fires a ticket's Warning alert once when it
    // crosses its assigned agent's own threshold, and its Breach alert once when it breaches
    // (a breach always alerts, regardless of the configured warning threshold).
    public async Task EvaluateAndSendAlertsAsync(CancellationToken ct)
    {
        var assignedOpenTickets = (await ticketRepository.GetOpenAsync(ct)).Where(t => t.AssignedAgentId is not null).ToList();
        foreach (var ticket in assignedOpenTickets)
        {
            var status = await slaCalculationService.GetStatusAsync(ticket.Id, ct);
            if (status is null) continue;

            var preference = await preferenceRepository.GetByAgentIdAsync(ticket.AssignedAgentId!.Value, ct) ?? new AlertPreference(ticket.AssignedAgentId.Value);
            var elapsedPercentage = 100 - status.ResolutionRemainingMinutes * 100 / Math.Max(1, status.ResolutionTargetMinutes);

            if (status.IsResolutionBreached)
                await SendOnceAsync(ticket, "Breach", $"Ticket {ticket.ReferenceNumber} has breached its SLA.", preference, ct);
            else if (elapsedPercentage >= preference.WarningThresholdPercentage)
                await SendOnceAsync(ticket, "Warning", $"Ticket {ticket.ReferenceNumber} is at risk of breaching its SLA ({status.ResolutionRemainingMinutes} min remaining).", preference, ct);
        }
    }

    public async Task SendDailyWeeklyDigestsAsync(CancellationToken ct)
    {
        var subscribed = await preferenceRepository.GetWithDigestEnabledAsync(ct);
        if (subscribed.Count == 0) return;

        var atRisk = await GetAtRiskTicketsAsync(ct);
        var now = timeProvider.GetUtcNow();
        foreach (var preference in subscribed)
        {
            var lastSent = await preferenceRepository.GetLastDigestSentAsync(preference.AgentId, ct);
            var interval = preference.DigestFrequency == DigestFrequency.Daily ? TimeSpan.FromDays(1) : TimeSpan.FromDays(7);
            if (lastSent is not null && now - lastSent.Value < interval) continue;

            await alertNotifier.SendDigestAsync(preference.AgentId, atRisk, ct);
            await preferenceRepository.AddDigestLogAsync(new DigestLogEntry(preference.AgentId, now), ct);
        }
        await preferenceRepository.SaveChangesAsync(ct);
    }

    private async Task SendOnceAsync(Ticket ticket, string kind, string message, AlertPreference preference, CancellationToken ct)
    {
        if (await preferenceRepository.HasAlertBeenSentAsync(ticket.Id, kind, ct)) return;

        await notificationService.NotifyAsync(ticket.AssignedAgentId!.Value, $"Sla{kind}", message, ticket.Id, ct);

        if (kind == "Breach")
            await alertNotifier.NotifyBreachAsync(ticket.AssignedAgentId.Value, ticket.Id, ticket.ReferenceNumber, ct);
        else if (preference.EmailEnabled || preference.PushEnabled)
            await alertNotifier.NotifyWarningAsync(ticket.AssignedAgentId.Value, ticket.Id, ticket.ReferenceNumber, ct);

        await preferenceRepository.AddAlertLogAsync(new SlaAlertLog(ticket.Id, kind, timeProvider.GetUtcNow()), ct);
        await preferenceRepository.SaveChangesAsync(ct);
    }

    private static AlertPreferenceDto ToDto(AlertPreference p) => new(p.AgentId, p.EmailEnabled, p.PushEnabled, p.WarningThresholdPercentage, p.DigestFrequency);
}
