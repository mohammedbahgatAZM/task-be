namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;
using SupportCrm.Application.Sla;
using SupportCrm.Application.Ai;
using SupportCrm.Application.KnowledgeBase;
using SupportCrm.Application.CustomerPortal;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

[ApiController]
[Route("api/tickets")]
public class TicketsController(TicketService ticketService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TicketDto>> Create([FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        try
        {
            var dto = await ticketService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketDto>> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            return await ticketService.GetByIdAsync(id, ct);
        }
        catch (TicketNotFoundException)
        {
            return NotFound();
        }
    }

    // Requester-facing lookup by reference number — no auth exists in this codebase yet,
    // so the reference number is the access credential for this story (see plan Edge Cases).
    [HttpGet("reference/{referenceNumber}/status")]
    public async Task<ActionResult<TicketStatusViewDto>> GetStatusByReference(string referenceNumber, CancellationToken ct)
    {
        try
        {
            return await ticketService.GetStatusByReferenceAsync(referenceNumber, ct);
        }
        catch (TicketNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id:guid}/category")]
    public async Task<IActionResult> SetCategory(Guid id, [FromBody] SetCategoryRequest request, CancellationToken ct)
    {
        try { await ticketService.SetCategoryAsync(id, request, ct); return NoContent(); }
        catch (TicketNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/priority")]
    public async Task<IActionResult> SetPriority(Guid id, [FromBody] SetPriorityRequest request, CancellationToken ct)
    {
        try { await ticketService.SetPriorityAsync(id, request, ct); return NoContent(); }
        catch (TicketNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:guid}/field-history")]
    public async Task<ActionResult<IReadOnlyList<TicketFieldChangeDto>>> GetFieldHistory(Guid id, CancellationToken ct) =>
        Ok(await ticketService.GetFieldChangeLogAsync(id, ct));

    [HttpGet("grouped-counts")]
    public async Task<ActionResult<TicketGroupedCountsDto>> GetGroupedCounts(CancellationToken ct) =>
        Ok(await ticketService.GetGroupedCountsAsync(ct));

    [HttpGet("{id:guid}/sla-status")]
    public async Task<ActionResult<TicketSlaStatusDto>> GetSlaStatus(Guid id, [FromServices] SlaCalculationService slaCalculationService, CancellationToken ct)
    {
        try
        {
            var status = await slaCalculationService.GetStatusAsync(id, ct);
            return status is null ? NotFound("No SLA target is configured for this ticket's priority.") : status;
        }
        catch (TicketNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/assignment")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignTicketRequest request, [FromServices] TicketAssignmentService assignmentService, CancellationToken ct)
    {
        try { await assignmentService.AssignAsync(id, request, ct); return NoContent(); }
        catch (TicketNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("unassigned")]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetUnassigned([FromServices] TicketAssignmentService assignmentService, CancellationToken ct) =>
        Ok(await assignmentService.GetUnassignedAsync(ct));

    [HttpGet("agent-load")]
    public async Task<ActionResult<IReadOnlyList<AgentLoadDto>>> GetAgentLoad([FromServices] TicketAssignmentService assignmentService, CancellationToken ct) =>
        Ok(await assignmentService.GetAgentLoadAsync(ct));

    [HttpGet("assigned")]
    public async Task<ActionResult<IReadOnlyList<AgentDashboardTicketDto>>> GetAssigned(
        [FromQuery] Guid agentId, [FromQuery] TicketStatus? status, [FromQuery] TicketPriority? priority, [FromQuery] Guid? categoryId,
        [FromServices] AgentDashboardService dashboardService, CancellationToken ct) =>
        Ok(await dashboardService.GetAssignedTicketsAsync(agentId, status, priority, categoryId, ct));

    [HttpPost("{id:guid}/tasks")]
    public async Task<ActionResult<TicketTaskDto>> CreateTask(Guid id, [FromBody] CreateTicketTaskRequest request, [FromServices] TicketTaskService taskService, CancellationToken ct) =>
        await taskService.CreateAsync(id, request, ct);

    [HttpGet("{id:guid}/tasks")]
    public async Task<ActionResult<IReadOnlyList<TicketTaskDto>>> GetTasks(Guid id, [FromServices] TicketTaskService taskService, CancellationToken ct) =>
        Ok(await taskService.GetForTicketAsync(id, ct));

    [HttpGet("tasks/overdue")]
    public async Task<ActionResult<IReadOnlyList<TicketTaskDto>>> GetOverdueTasks([FromQuery] Guid agentId, [FromServices] TicketTaskService taskService, CancellationToken ct) =>
        Ok(await taskService.GetOverdueForAgentAsync(agentId, ct));

    [HttpPut("tasks/{taskId:guid}/complete")]
    public async Task<IActionResult> CompleteTask(Guid taskId, [FromServices] TicketTaskService taskService, CancellationToken ct)
    {
        try { await taskService.CompleteAsync(taskId, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("tasks/{taskId:guid}/reassign")]
    public async Task<IActionResult> ReassignTask(Guid taskId, [FromBody] ReassignTicketTaskRequest request, [FromServices] TicketTaskService taskService, CancellationToken ct)
    {
        try { await taskService.ReassignAsync(taskId, request.NewAgentId, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetTicketStatusRequest request, CancellationToken ct)
    {
        try { await ticketService.SetStatusAsync(id, request, ct); return NoContent(); }
        catch (TicketNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/escalate")]
    public async Task<IActionResult> Escalate(Guid id, [FromBody] EscalateTicketRequest request, [FromServices] TicketEscalationService escalationService, CancellationToken ct)
    {
        try { await escalationService.EscalateAsync(id, request, ct); return NoContent(); }
        catch (TicketNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("{id:guid}/escalations")]
    public async Task<ActionResult<IReadOnlyList<TicketEscalationDto>>> GetEscalations(Guid id, [FromServices] TicketEscalationService escalationService, CancellationToken ct) =>
        Ok(await escalationService.GetEscalationsAsync(id, ct));

    [HttpGet("{id:guid}/escalation-log")]
    public async Task<ActionResult<IReadOnlyList<EscalationLogEntryDto>>> GetEscalationLog(Guid id, [FromServices] EscalationRuleService ruleService, CancellationToken ct) =>
        Ok(await ruleService.GetLogForTicketAsync(id, ct));

    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<TicketMessageDto>> AddMessage(Guid id, [FromBody] AddTicketMessageRequest request, [FromServices] TicketMessageService messageService, CancellationToken ct)
    {
        try { return await messageService.AddMessageAsync(id, request, ct); }
        catch (TicketNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/notes")]
    public async Task<ActionResult<TicketNoteDto>> AddNote(
        Guid id, [FromBody] AddTicketNoteRequest request, [FromServices] TicketMessageService messageService,
        [FromServices] TicketCollaborationService collaborationService, CancellationToken ct)
    {
        try
        {
            var note = await messageService.AddNoteAsync(id, request, ct);
            await collaborationService.ProcessMentionsAsync(id, note.Text, ct);
            return note;
        }
        catch (TicketNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:guid}/collaborators")]
    public async Task<ActionResult<IReadOnlyList<TicketCollaboratorDto>>> GetCollaborators(
        Guid id, [FromServices] TicketCollaborationService collaborationService, CancellationToken ct) =>
        Ok(await collaborationService.GetForTicketAsync(id, ct));

    [HttpPost("{id:guid}/collaborators")]
    public async Task<IActionResult> AddCollaborator(
        Guid id, [FromBody] AddTicketCollaboratorRequest request, [FromServices] TicketCollaborationService collaborationService, CancellationToken ct)
    {
        await collaborationService.AddCollaboratorAsync(id, request.AgentId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult<IReadOnlyList<TicketTimelineEntryDto>>> GetTimeline(Guid id, [FromServices] TicketTimelineService timelineService, CancellationToken ct)
    {
        try { return Ok(await timelineService.GetTimelineAsync(id, ct)); }
        catch (TicketNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/email-replies")]
    public async Task<ActionResult<TicketMessageDto>> SendEmailReply(Guid id, [FromBody] SendEmailReplyRequest request, [FromServices] EmailChannelService emailChannelService, CancellationToken ct)
    {
        try { return await emailChannelService.SendReplyAsync(id, request, ct); }
        catch (TicketNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("{id:guid}/delivery-statuses")]
    public async Task<ActionResult<IReadOnlyList<TicketMessageDeliveryStatusDto>>> GetDeliveryStatuses(Guid id, [FromServices] EmailChannelService emailChannelService, CancellationToken ct) =>
        Ok(await emailChannelService.GetDeliveryStatusesAsync(id, ct));

    [HttpPost("{id:guid}/whatsapp-messages")]
    public async Task<ActionResult<TicketMessageDto>> SendWhatsAppMessage(Guid id, [FromBody] SendWhatsAppMessageRequest request, [FromServices] WhatsAppChannelService whatsAppChannelService, CancellationToken ct)
    {
        try { return await whatsAppChannelService.SendAsync(id, request, ct); }
        catch (TicketNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{id:guid}/sms-messages")]
    public async Task<ActionResult<TicketMessageDto>> SendSms(Guid id, [FromBody] SendSmsRequest request, [FromServices] SmsChannelService smsChannelService, CancellationToken ct)
    {
        try { return await smsChannelService.SendAsync(id, request, ct); }
        catch (TicketNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{id:guid}/reply")]
    public async Task<ActionResult<TicketMessageDto>> Reply(Guid id, [FromBody] DispatchReplyRequest request, [FromServices] ChannelReplyDispatcher dispatcher, CancellationToken ct)
    {
        try { return await dispatcher.ReplyAsync(id, request, ct); }
        catch (TicketNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{id:guid}/attachments")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<TicketAttachmentDto>> UploadAttachment(Guid id, IFormFile file, [FromQuery] string? uploadedByName, [FromServices] TicketAttachmentService attachmentService, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("A file is required.");
        try
        {
            await using var stream = file.OpenReadStream();
            return await attachmentService.AddAsync(id, file.FileName, file.ContentType, file.Length, stream, uploadedByName ?? "unknown", ct);
        }
        catch (TicketNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:guid}/attachments")]
    public async Task<ActionResult<IReadOnlyList<TicketAttachmentDto>>> GetAttachments(Guid id, [FromServices] TicketAttachmentService attachmentService, CancellationToken ct) =>
        Ok(await attachmentService.GetForTicketAsync(id, ct));

    [HttpGet("attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId, [FromServices] TicketAttachmentService attachmentService, CancellationToken ct)
    {
        try
        {
            var (content, attachment) = await attachmentService.OpenAsync(attachmentId, ct);
            return File(content, attachment.ContentType, attachment.FileName);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // AI-1 — ticket summaries
    [HttpGet("{id:guid}/message-count")]
    public async Task<ActionResult<int>> GetMessageCount(Guid id, [FromServices] ITicketMessageRepository messageRepository, CancellationToken ct) =>
        Ok(await messageRepository.CountByTicketAsync(id, ct));

    [HttpGet("{id:guid}/ai-summary")]
    public async Task<ActionResult<TicketAiSummaryDto>> GetAiSummary(Guid id, [FromServices] TicketSummaryService summaryService, CancellationToken ct)
    {
        var summary = await summaryService.GetAsync(id, ct);
        return summary is null ? NotFound() : summary;
    }

    [HttpPost("{id:guid}/ai-summary")]
    public async Task<ActionResult<TicketAiSummaryDto>> GenerateAiSummary(Guid id, [FromServices] TicketSummaryService summaryService, CancellationToken ct)
    {
        try { return await summaryService.GenerateAsync(id, ct); }
        catch (TicketNotFoundForAiException) { return NotFound(); }
    }

    // AI-2 — suggested replies
    [HttpPost("{id:guid}/ai-reply-draft")]
    public async Task<ActionResult<AiReplyDraftDto>> GetAiReplyDraft(Guid id, [FromServices] AiReplyDraftService draftService, CancellationToken ct)
    {
        try { return await draftService.DraftAsync(id, ct); }
        catch (TicketNotFoundForAiException) { return NotFound(); }
    }

    // AI-3 — automatic categorization
    [HttpGet("{id:guid}/ai-categorization-suggestion")]
    public async Task<ActionResult<TicketCategorizationSuggestionDto>> GetAiCategorizationSuggestion(Guid id, [FromServices] TicketCategorizationService categorizationService, CancellationToken ct)
    {
        var suggestion = await categorizationService.GetSuggestionAsync(id, ct);
        return suggestion is null ? NotFound() : suggestion;
    }

    // AI-4 — suggested solutions
    [HttpGet("{id:guid}/solution-suggestions")]
    public async Task<ActionResult<IReadOnlyList<KbSearchResultDto>>> GetSolutionSuggestions(Guid id, [FromServices] TicketSolutionSuggestionService suggestionService, CancellationToken ct)
    {
        try { return Ok(await suggestionService.GetSuggestionsAsync(id, ct)); }
        catch (TicketNotFoundForAiException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/solution-suggestions/feedback")]
    public async Task<IActionResult> FlagSolutionSuggestion(Guid id, [FromBody] FlagSolutionSuggestionRequest request, [FromServices] TicketSolutionSuggestionService suggestionService, CancellationToken ct)
    {
        await suggestionService.FlagIrrelevantAsync(id, request, ct);
        return NoContent();
    }

    // CP-2 — customer-authored inbound reply, distinct from the agent-outbound /reply endpoint
    [HttpPost("{id:guid}/portal-reply")]
    public async Task<ActionResult<TicketMessageDto>> AddPortalReply(Guid id, [FromBody] AddPortalReplyRequest request, [FromServices] CustomerPortalTicketService portalTicketService, CancellationToken ct)
    {
        try { return await portalTicketService.AddPortalReplyAsync(id, request, ct); }
        catch (TicketNotFoundException) { return NotFound(); }
        catch (TicketOwnershipException) { return Forbid(); }
    }

    // CP-3 — customer self-service reopen, within a configurable window
    [HttpPost("{id:guid}/reopen")]
    public async Task<IActionResult> Reopen(Guid id, [FromBody] ReopenTicketRequest request, [FromServices] CustomerPortalTicketService portalTicketService, CancellationToken ct)
    {
        try { await portalTicketService.ReopenAsync(id, request, ct); return NoContent(); }
        catch (TicketNotFoundException) { return NotFound(); }
        catch (TicketOwnershipException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    // CP-5 — post-resolution CSAT rating; a low rating creates a supervisor follow-up task
    [HttpPost("{id:guid}/feedback")]
    public async Task<ActionResult<TicketFeedbackDto>> SubmitFeedback(Guid id, [FromBody] SubmitTicketFeedbackRequest request, [FromServices] TicketFeedbackService feedbackService, CancellationToken ct)
    {
        try { return await feedbackService.SubmitAsync(id, request, ct); }
        catch (TicketNotFoundException) { return NotFound(); }
        catch (TicketOwnershipException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("{id:guid}/feedback")]
    public async Task<ActionResult<TicketFeedbackDto>> GetFeedback(Guid id, [FromServices] TicketFeedbackService feedbackService, CancellationToken ct)
    {
        var feedback = await feedbackService.GetAsync(id, ct);
        return feedback is null ? NotFound() : feedback;
    }
}
