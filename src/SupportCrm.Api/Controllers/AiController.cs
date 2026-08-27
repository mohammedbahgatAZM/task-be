namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Ai;

[ApiController]
[Route("api/ai")]
public class AiController(TicketCategorizationService categorizationService) : ControllerBase
{
    [HttpGet("categorization/pending")]
    public async Task<ActionResult<IReadOnlyList<Guid>>> GetPendingCategorization(CancellationToken ct) =>
        Ok(await categorizationService.GetPendingManualCategorizationTicketIdsAsync(ct));

    [HttpGet("categorization/accuracy-report")]
    public async Task<ActionResult<IReadOnlyList<CategorizationAccuracyPointDto>>> GetAccuracyReport(CancellationToken ct) =>
        Ok(await categorizationService.GetAccuracyReportAsync(ct));
}
