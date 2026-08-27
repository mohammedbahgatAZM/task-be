namespace SupportCrm.Domain.Entities;

// One row per scope — BranchId == null is the global default; a non-null BranchId overrides it
// for that branch only. Uniqueness per scope is enforced at the database level (a unique index
// on BranchId — Postgres treats multiple NULLs as distinct in a plain unique index, but since
// there is only ever meant to be one global row, the repository/service layer enforces that by
// always looking up-then-updating rather than blind-inserting).
public class BrandingSettings
{
    public Guid Id { get; private set; }
    public Guid? BranchId { get; private set; }
    public string? LogoStorageKey { get; private set; }
    public string? LogoContentType { get; private set; }
    public string PrimaryColorHex { get; private set; } = default!;
    public string SecondaryColorHex { get; private set; } = default!;
    public string UpdatedBy { get; private set; } = default!;
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private BrandingSettings() { }

    public BrandingSettings(Guid? branchId, string? logoStorageKey, string? logoContentType, string primaryColorHex, string secondaryColorHex, string updatedBy, DateTimeOffset updatedAtUtc)
    {
        Id = Guid.NewGuid();
        BranchId = branchId;
        LogoStorageKey = logoStorageKey;
        LogoContentType = logoContentType;
        PrimaryColorHex = primaryColorHex;
        SecondaryColorHex = secondaryColorHex;
        UpdatedBy = updatedBy;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Update(string? logoStorageKey, string? logoContentType, string primaryColorHex, string secondaryColorHex, string updatedBy, DateTimeOffset updatedAtUtc)
    {
        LogoStorageKey = logoStorageKey;
        LogoContentType = logoContentType;
        PrimaryColorHex = primaryColorHex;
        SecondaryColorHex = secondaryColorHex;
        UpdatedBy = updatedBy;
        UpdatedAtUtc = updatedAtUtc;
    }
}
