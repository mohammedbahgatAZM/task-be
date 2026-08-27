namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Sla;

[ApiController]
[Route("api/sla")]
public class SlaController(SlaTargetService targetService, BusinessCalendarConfigService calendarConfigService) : ControllerBase
{
    [HttpGet("targets")]
    public async Task<ActionResult<IReadOnlyList<SlaTargetDto>>> GetTargets(CancellationToken ct) =>
        Ok(await targetService.GetActiveAsync(ct));

    [HttpPost("targets")]
    public async Task<ActionResult<SlaTargetDto>> CreateTarget([FromBody] CreateSlaTargetRequest request, CancellationToken ct)
    {
        try { return await targetService.CreateAsync(request, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("business-hours")]
    public async Task<ActionResult<IReadOnlyList<BusinessHoursDto>>> GetBusinessHours(CancellationToken ct) =>
        Ok(await calendarConfigService.GetBusinessHoursAsync(ct));

    [HttpPut("business-hours")]
    public async Task<IActionResult> SetBusinessHours([FromBody] SetBusinessHoursRequest request, CancellationToken ct)
    {
        try { await calendarConfigService.SetBusinessHoursAsync(request, ct); return NoContent(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("holidays")]
    public async Task<ActionResult<IReadOnlyList<HolidayDto>>> GetHolidays(CancellationToken ct) =>
        Ok(await calendarConfigService.GetHolidaysAsync(ct));

    [HttpPost("holidays")]
    public async Task<ActionResult<HolidayDto>> AddHoliday([FromBody] CreateHolidayRequest request, CancellationToken ct)
    {
        try { return await calendarConfigService.AddHolidayAsync(request, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }
}
