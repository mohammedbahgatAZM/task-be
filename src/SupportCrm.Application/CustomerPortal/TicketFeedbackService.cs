namespace SupportCrm.Application.CustomerPortal;

using Microsoft.Extensions.Options;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Tickets;

public class TicketFeedbackService(
    ITicketRepository ticketRepository,
    ITicketFeedbackRepository feedbackRepository,
    IAgentRepository agentRepository,
    TicketTaskService taskService,
    IOptions<CustomerPortalOptions> options,
    TimeProvider timeProvider)
{
    public async Task<TicketFeedbackDto> SubmitAsync(Guid ticketId, SubmitTicketFeedbackRequest request, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        if (ticket.CustomerId != request.CustomerId)
            throw new TicketOwnershipException(ticketId);

        if (await feedbackRepository.GetByTicketAsync(ticketId, ct) is not null)
            throw new InvalidOperationException("Feedback has already been submitted for this ticket.");

        var feedback = new TicketFeedback(ticketId, request.Rating, request.Comment?.Trim(), timeProvider.GetUtcNow());
        await feedbackRepository.AddAsync(feedback, ct);
        await feedbackRepository.SaveChangesAsync(ct);

        if (request.Rating <= options.Value.LowRatingThreshold)
            await CreateSupervisorFollowUpAsync(ticket, feedback, ct);

        return ToDto(feedback);
    }

    public async Task<TicketFeedbackDto?> GetAsync(Guid ticketId, CancellationToken ct)
    {
        var feedback = await feedbackRepository.GetByTicketAsync(ticketId, ct);
        return feedback is null ? null : ToDto(feedback);
    }

    // Assigns ONE supervisor, not all of them — a task needs a single clear owner, unlike SLA &
    // Automation's escalation tiers (Story 23), which deliberately notify every supervisor.
    private async Task CreateSupervisorFollowUpAsync(Ticket ticket, TicketFeedback feedback, CancellationToken ct)
    {
        var agents = await agentRepository.GetAllAsync(ct);
        var supervisor = agents.FirstOrDefault(a => a.IsSupervisor);
        if (supervisor is null) return; // no supervisor staffed — skipped, not queued/retried

        await taskService.CreateAsync(ticket.Id, new CreateTicketTaskRequest(
            $"Low CSAT rating ({feedback.Rating}/5) on ticket {ticket.ReferenceNumber} — follow up.",
            timeProvider.GetUtcNow().AddDays(1),
            supervisor.Id,
            "System"), ct);
    }

    private static TicketFeedbackDto ToDto(TicketFeedback f) => new(f.TicketId, f.Rating, f.Comment, f.SubmittedAtUtc);
}
