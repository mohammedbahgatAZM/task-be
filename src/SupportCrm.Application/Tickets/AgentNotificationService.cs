namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AgentNotificationService(
    IAgentNotificationRepository notificationRepository,
    ITicketTaskRepository taskRepository,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<AgentNotificationDto>> GetForAgentAsync(Guid agentId, CancellationToken ct)
    {
        await MaterializeDueTaskNotificationsAsync(agentId, ct);
        return (await notificationRepository.GetByAgentAsync(agentId, ct))
            .OrderByDescending(n => n.CreatedAtUtc)
            .Select(ToDto)
            .ToList();
    }

    public async Task MarkReadAsync(Guid notificationId, CancellationToken ct)
    {
        var notification = await notificationRepository.GetByIdAsync(notificationId, ct)
            ?? throw new KeyNotFoundException($"Notification '{notificationId}' was not found.");
        notification.MarkRead();
        await notificationRepository.SaveChangesAsync(ct);
    }

    // Reused as-is by Agent Dashboard AD-5 (@-mentions) — the one shared way any part of
    // this app creates an agent notification. Do not add a second, parallel mechanism there.
    public async Task NotifyAsync(Guid agentId, string kind, string message, Guid? relatedTicketId, CancellationToken ct)
    {
        await notificationRepository.AddAsync(new AgentNotification(agentId, kind, message, relatedTicketId, timeProvider.GetUtcNow()), ct);
        await notificationRepository.SaveChangesAsync(ct);
    }

    private async Task MaterializeDueTaskNotificationsAsync(Guid agentId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var dueTasks = (await taskRepository.GetByAgentAsync(agentId, ct))
            .Where(t => !t.IsCompleted && t.NotifiedAtUtc is null && t.DueAtUtc <= now)
            .ToList();

        if (dueTasks.Count == 0) return;

        foreach (var task in dueTasks)
        {
            await notificationRepository.AddAsync(new AgentNotification(agentId, "TaskDue", $"Task due: {task.Note}", task.TicketId, now), ct);
            task.MarkNotified(now);
        }
        await taskRepository.SaveChangesAsync(ct);
    }

    private static AgentNotificationDto ToDto(AgentNotification n) => new(n.Id, n.Kind, n.Message, n.RelatedTicketId, n.IsRead, n.CreatedAtUtc);
}
