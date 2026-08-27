namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.KnowledgeBase;

[ApiController]
[Route("api/kb/content")]
public class KbContentReviewController(ContentWorkflowService workflowService) : ControllerBase
{
    [HttpGet("due-for-review")]
    public async Task<ActionResult<IReadOnlyList<DueForReviewItemDto>>> GetDueForReview(CancellationToken ct) =>
        Ok(await workflowService.GetDueForReviewAsync(ct));
}
