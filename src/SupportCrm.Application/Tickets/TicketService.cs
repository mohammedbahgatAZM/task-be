namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Ai;
using SupportCrm.Application.Platform;
using SupportCrm.Application.Integrations;

public class TicketService(
    ITicketRepository ticketRepository,
    TicketCustomerResolver customerResolver,
    ICustomerStatusNotifier customerStatusNotifier,
    AssignmentRuleEngine assignmentRuleEngine,
    TicketCategorizationService categorizationService,
    TicketDepartmentRoutingService departmentRoutingService,
    WebhookService webhookService,
    TimeProvider timeProvider)
{
    public async Task<TicketDto> CreateAsync(CreateTicketRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new ArgumentException("Subject is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.RequesterName))
            throw new ArgumentException("Requester name is required.", nameof(request));

        var customerId = request.CustomerId ?? await customerResolver.ResolveCustomerIdAsync(request.RequesterName, request.RequesterContactValue, ct);
        var now = timeProvider.GetUtcNow();
        var referenceNumber = $"TCK-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        var ticket = new Ticket(referenceNumber, customerId, request.Channel, request.Subject.Trim(),
            request.Description?.Trim(), request.RequesterName.Trim(), request.RequesterContactValue?.Trim(), now);
        ticket.SetLanguage(request.Language?.Trim());

        // CP-1: an explicit customer-selected category wins outright over AI-3's guess.
        IReadOnlyList<TicketFieldChangeEntry> aiFieldChanges = Array.Empty<TicketFieldChangeEntry>();
        if (request.CategoryId is not null)
            ticket.SetCategory(request.CategoryId);
        else
            aiFieldChanges = await categorizationService.CategorizeOnCreateAsync(ticket, ct);

        // Platform PL-3: category-based department routing wins; a channel-default department
        // is the fallback. Neither matching leaves DepartmentId null (unrouted, not an error).
        ticket.SetDepartment(await departmentRoutingService.ResolveDepartmentAsync(ticket.CategoryId, request.Channel, ct));

        await ticketRepository.AddAsync(ticket, ct);
        await ticketRepository.AddStatusChangeAsync(
            new TicketStatusChangeEntry(ticket.Id, null, TicketStatus.New, request.CreatedBy, "Agent", null, now), ct);
        foreach (var change in aiFieldChanges)
            await ticketRepository.AddFieldChangeAsync(change, ct);
        await ticketRepository.SaveChangesAsync(ct);

        await assignmentRuleEngine.EvaluateAndAssignAsync(ticket.Id, ct);

        // INT-1 — "webhooks ... to notify external systems of key events (e.g. ticket
        // created/resolved)." A no-op when nobody is subscribed to ticket.created.
        await webhookService.DispatchAsync(WebhookEventTypes.TicketCreated,
            new { ticket.Id, ticket.ReferenceNumber, ticket.CustomerId, Channel = ticket.Channel.ToString(), ticket.Subject, Status = ticket.Status.ToString(), ticket.CreatedAtUtc }, ct);

        return ToDto(ticket);
    }

    public async Task<TicketDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(id, ct) ?? throw new TicketNotFoundException(id.ToString());
        return ToDto(ticket);
    }

    public async Task<TicketStatusViewDto> GetStatusByReferenceAsync(string referenceNumber, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByReferenceNumberAsync(referenceNumber, ct)
            ?? throw new TicketNotFoundException(referenceNumber);
        var history = await ticketRepository.GetStatusHistoryAsync(ticket.Id, ct);
        var lastUpdate = history.Count > 0 ? history.Max(h => h.ChangedAtUtc) : ticket.CreatedAtUtc;
        return new TicketStatusViewDto(ticket.ReferenceNumber, ticket.Status, lastUpdate);
    }

    /// <summary>
    /// Records a status change + audit entry. Internal building block for TM-4's public
    /// status-change/escalation endpoints — TM-1 only calls it once, for ticket creation.
    /// </summary>
    public async Task RecordStatusChangeAsync(Guid ticketId, TicketStatus newStatus, string changedBy, string changedByKind, string? reason, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var now = timeProvider.GetUtcNow();
        var oldStatus = ticket.Status;
        ticket.SetStatus(newStatus, now);
        await ticketRepository.AddStatusChangeAsync(
            new TicketStatusChangeEntry(ticketId, oldStatus, newStatus, changedBy, changedByKind, reason, now), ct);
        await ticketRepository.SaveChangesAsync(ct);

        // INT-1 — fires once, on the transition INTO Resolved (not on every save while already
        // resolved, and not on Closed — the story's own example event is "ticket ... resolved").
        if (newStatus == TicketStatus.Resolved && oldStatus != TicketStatus.Resolved)
            await webhookService.DispatchAsync(WebhookEventTypes.TicketResolved,
                new { ticket.Id, ticket.ReferenceNumber, ticket.CustomerId, Status = newStatus.ToString(), ResolvedAtUtc = now }, ct);
    }

    public async Task SetStatusAsync(Guid ticketId, SetTicketStatusRequest request, CancellationToken ct)
    {
        await RecordStatusChangeAsync(ticketId, request.NewStatus, request.ChangedBy, "Agent", request.Reason, ct);
        if (request.NotifyCustomer)
            await customerStatusNotifier.NotifyStatusChangedAsync(ticketId, request.NewStatus, ct);
    }

    public async Task SetCategoryAsync(Guid ticketId, SetCategoryRequest request, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var oldCategoryId = ticket.CategoryId;
        ticket.SetCategory(request.CategoryId);
        await ticketRepository.AddFieldChangeAsync(
            new TicketFieldChangeEntry(ticketId, "Category", oldCategoryId?.ToString(), request.CategoryId?.ToString(), request.ChangedBy, timeProvider.GetUtcNow()), ct);
        await ticketRepository.SaveChangesAsync(ct);
    }

    public async Task SetPriorityAsync(Guid ticketId, SetPriorityRequest request, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var oldPriority = ticket.Priority;
        ticket.SetPriority(request.Priority);
        await ticketRepository.AddFieldChangeAsync(
            new TicketFieldChangeEntry(ticketId, "Priority", oldPriority.ToString(), request.Priority.ToString(), request.ChangedBy, timeProvider.GetUtcNow()), ct);
        await ticketRepository.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TicketFieldChangeDto>> GetFieldChangeLogAsync(Guid ticketId, CancellationToken ct) =>
        (await ticketRepository.GetFieldChangeLogAsync(ticketId, ct))
            .OrderByDescending(e => e.ChangedAtUtc)
            .Select(e => new TicketFieldChangeDto(e.Id, e.FieldName, e.OldValue, e.NewValue, e.ChangedBy, e.ChangedAtUtc))
            .ToList();

    public async Task<TicketGroupedCountsDto> GetGroupedCountsAsync(CancellationToken ct)
    {
        var byCategory = await ticketRepository.CountGroupedByCategoryAsync(ct);
        var byPriority = await ticketRepository.CountGroupedByPriorityAsync(ct);
        return new TicketGroupedCountsDto(
            byCategory,
            byPriority.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value));
    }

    private static TicketDto ToDto(Ticket t) => new(t.Id, t.ReferenceNumber, t.CustomerId, t.Channel, t.Subject, t.Description, t.Status, t.CreatedAtUtc, t.ClosedAtUtc, t.CategoryId, t.Priority, t.DepartmentId);
}
