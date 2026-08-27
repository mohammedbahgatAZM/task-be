namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketTaskService(ITicketTaskRepository repository, TimeProvider timeProvider)
{
    public async Task<TicketTaskDto> CreateAsync(Guid ticketId, CreateTicketTaskRequest request, CancellationToken ct)
    {
        var task = new TicketTask(ticketId, request.Note, request.DueAtUtc, request.AssignedAgentId, request.CreatedBy, timeProvider.GetUtcNow());
        await repository.AddAsync(task, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(task);
    }

    public async Task<IReadOnlyList<TicketTaskDto>> GetForTicketAsync(Guid ticketId, CancellationToken ct) =>
        (await repository.GetByTicketAsync(ticketId, ct)).OrderBy(t => t.DueAtUtc).Select(ToDto).ToList();

    public async Task<IReadOnlyList<TicketTaskDto>> GetOverdueForAgentAsync(Guid agentId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        return (await repository.GetByAgentAsync(agentId, ct))
            .Where(t => !t.IsCompleted && t.DueAtUtc < now)
            .OrderBy(t => t.DueAtUtc)
            .Select(ToDto)
            .ToList();
    }

    public async Task CompleteAsync(Guid taskId, CancellationToken ct)
    {
        var task = await repository.GetByIdAsync(taskId, ct) ?? throw new KeyNotFoundException($"Task '{taskId}' was not found.");
        task.Complete();
        await repository.SaveChangesAsync(ct);
    }

    public async Task ReassignAsync(Guid taskId, Guid newAgentId, CancellationToken ct)
    {
        var task = await repository.GetByIdAsync(taskId, ct) ?? throw new KeyNotFoundException($"Task '{taskId}' was not found.");
        task.Reassign(newAgentId);
        await repository.SaveChangesAsync(ct);
    }

    private static TicketTaskDto ToDto(TicketTask t) => new(t.Id, t.TicketId, t.Note, t.DueAtUtc, t.AssignedAgentId, t.IsCompleted, t.CreatedAtUtc);
}
