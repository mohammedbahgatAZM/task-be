namespace SupportCrm.Application.Platform;

using System.Text.RegularExpressions;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public partial class BrandingService(IBrandingSettingsRepository repository, TimeProvider timeProvider)
{
    // This app's own existing brand colors (frontend/src/styles.scss $primary/$secondary) — the
    // "nothing configured yet" fallback, so an unbranded system still looks like this app.
    private const string DefaultPrimary = "#1565c0";
    private const string DefaultSecondary = "#5e35b1";

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColorPattern();

    public BrandingValidationDto Validate(BrandingPreviewRequest request)
    {
        var errors = new Dictionary<string, string>();
        if (!HexColorPattern().IsMatch(request.PrimaryColorHex)) errors["primaryColorHex"] = "Must be a hex color like #1565c0.";
        if (!HexColorPattern().IsMatch(request.SecondaryColorHex)) errors["secondaryColorHex"] = "Must be a hex color like #5e35b1.";
        return new BrandingValidationDto(errors.Count == 0, errors);
    }

    public async Task<BrandingSettingsDto> PublishAsync(BrandingPreviewRequest request, string publishedBy, CancellationToken ct)
    {
        var validation = Validate(request);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors.Values));

        var now = timeProvider.GetUtcNow();
        var existing = await repository.GetByBranchIdAsync(request.BranchId, ct);
        if (existing is null)
        {
            var created = new BrandingSettings(request.BranchId, request.LogoStorageKey, null, request.PrimaryColorHex, request.SecondaryColorHex, publishedBy, now);
            await repository.AddAsync(created, ct);
        }
        else
        {
            existing.Update(request.LogoStorageKey, existing.LogoContentType, request.PrimaryColorHex, request.SecondaryColorHex, publishedBy, now);
        }
        await repository.SaveChangesAsync(ct);
        return await GetEffectiveAsync(request.BranchId, ct);
    }

    // Resolution order: this branch's own override -> the global (BranchId=null) row -> this
    // app's own hardcoded defaults. IsDefault=true means "nothing was ever configured," so the
    // frontend can distinguish a deliberately-unbranded system from one that just hasn't loaded yet.
    public async Task<BrandingSettingsDto> GetEffectiveAsync(Guid? branchId, CancellationToken ct)
    {
        var branchSpecific = branchId is not null ? await repository.GetByBranchIdAsync(branchId, ct) : null;
        var settings = branchSpecific ?? await repository.GetByBranchIdAsync(null, ct);
        if (settings is null) return new BrandingSettingsDto(branchId, null, DefaultPrimary, DefaultSecondary, true);
        return new BrandingSettingsDto(settings.BranchId, settings.LogoStorageKey, settings.PrimaryColorHex, settings.SecondaryColorHex, false);
    }
}
