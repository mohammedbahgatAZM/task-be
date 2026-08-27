namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Platform;

[ApiController]
[Route("api/branding")]
public class BrandingController(BrandingService brandingService, IBrandingAssetStorage assetStorage) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BrandingSettingsDto>> GetEffective([FromQuery] Guid? branchId, CancellationToken ct) =>
        await brandingService.GetEffectiveAsync(branchId, ct);

    [HttpPost("preview")]
    public ActionResult<BrandingValidationDto> Preview([FromBody] BrandingPreviewRequest request) => brandingService.Validate(request);

    [HttpPost("publish")]
    public async Task<ActionResult<BrandingSettingsDto>> Publish([FromBody] BrandingPreviewRequest request, CancellationToken ct)
    {
        try { return await brandingService.PublishAsync(request, "admin", ct); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("logo")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<string>> UploadLogo(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("A file is required.");
        await using var stream = file.OpenReadStream();
        return await assetStorage.SaveAsync(file.FileName, stream, ct);
    }

    [HttpGet("logo/{storageKey}")]
    public async Task<IActionResult> DownloadLogo(string storageKey, CancellationToken ct)
    {
        try { return File(await assetStorage.OpenReadAsync(storageKey, ct), "image/*"); }
        catch (FileNotFoundException) { return NotFound(); }
    }
}
