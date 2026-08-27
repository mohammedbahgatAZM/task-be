namespace SupportCrm.Application.Platform;

using SupportCrm.Domain.Entities;

// PL-3 — departments
public record CreateDepartmentRequest(string Name);
public record DepartmentDto(Guid Id, string Name, bool IsActive, TicketChannel? DefaultForChannel);
public record SetDepartmentChannelRequest(TicketChannel? Channel);
public record SetDepartmentIdRequest(Guid? DepartmentId);

// PL-4 — branches
public record CreateBranchRequest(string Name, string Code);
public record BranchDto(Guid Id, string Name, string Code, string? DefaultLanguage, string? ContactNumber, bool IsActive);
public record UpdateBranchSettingsRequest(string? DefaultLanguage, string? ContactNumber);
public record SetBranchIdRequest(Guid? BranchId);

// PL-5 — branding
public record BrandingPreviewRequest(Guid? BranchId, string? LogoStorageKey, string PrimaryColorHex, string SecondaryColorHex);
public record BrandingValidationDto(bool IsValid, IReadOnlyDictionary<string, string> Errors);
public record BrandingSettingsDto(Guid? BranchId, string? LogoStorageKey, string PrimaryColorHex, string SecondaryColorHex, bool IsDefault);
