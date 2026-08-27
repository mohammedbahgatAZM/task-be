namespace SupportCrm.Application.Security;

// SEC-1 — users & auth
public record CreateUserRequest(string Email, string InitialPassword, IReadOnlyList<Guid> RoleIds);
public record UserDto(Guid Id, string Email, bool IsActive, bool MfaEnabled, DateTimeOffset PasswordChangedAtUtc, DateTimeOffset CreatedAtUtc, IReadOnlyList<string> RoleNames);
public record SetUserRolesRequest(IReadOnlyList<Guid> RoleIds);

public record LoginRequest(string Email, string Password);
public enum LoginOutcome { Success, RequiresMfa, InvalidCredentials, AccountLocked, AccountDeactivated, PasswordExpired }
public record LoginResultDto(LoginOutcome Outcome, string? AccessToken, DateTimeOffset? ExpiresAtUtc, string? MfaChallengeToken, string? PasswordResetChallengeToken = null);
public record CompleteMfaLoginRequest(string MfaChallengeToken, string Code);
public record CompleteExpiredPasswordChangeRequest(string PasswordResetChallengeToken, string NewPassword);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record MfaSetupDto(string Secret, string ProvisioningUri);
public record ConfirmMfaRequest(string Code);
public record CurrentUserDto(Guid UserId, string Email, IReadOnlyList<string> RoleNames, IReadOnlyList<string> Permissions);

public class SecurityValidationException(IReadOnlyList<string> errors) : Exception(string.Join(" ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
public class UserNotFoundException(Guid id) : Exception($"User '{id}' was not found.");
public class DuplicateEmailException(string email) : Exception($"A user with email '{email}' already exists.");

// SEC-2 — roles & permissions
public record PermissionDto(Guid Id, string Module, string Action);
public record CreateRoleRequest(string Name);
public record RoleDto(Guid Id, string Name, bool IsSystemDefined, IReadOnlyList<Guid> PermissionIds);
public record SetRolePermissionsRequest(IReadOnlyList<Guid> PermissionIds);
public class SystemRoleDeletionException(string name) : Exception($"Role '{name}' is system-defined and cannot be deleted.");
public class RoleNotFoundException(Guid id) : Exception($"Role '{id}' was not found.");

// SEC-3 — audit log
public record AuditLogQuery(Guid? UserId, DateTimeOffset? From, DateTimeOffset? To, string? ActionType);
public record AuditLogEntryDto(Guid Id, Guid? UserId, string UserEmail, string HttpMethod, string Path, string ActionSummary, string? IpAddress, DateTimeOffset OccurredAtUtc);

// SEC-4 — system configuration
public record SystemSettingDto(string Key, string DisplayName, string ValueType, string? Value);
public record ValidateSettingsRequest(IReadOnlyDictionary<string, string> Changes);
public record ValidationResultDto(bool IsValid, IReadOnlyDictionary<string, string> Errors);
public record ApplySettingsRequest(IReadOnlyDictionary<string, string> Changes);
