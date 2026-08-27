namespace SupportCrm.Application.Security;

using Microsoft.Extensions.Options;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AuthService(
    IUserRepository userRepository, IRoleRepository roleRepository,
    PasswordHashingService passwordHasher, PasswordPolicyValidator passwordPolicyValidator,
    TotpService totpService, JwtTokenService tokenService,
    IOptions<SecurityOptions> securityOptions, TimeProvider timeProvider)
{
    public async Task<LoginResultDto> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);
        var now = timeProvider.GetUtcNow();
        if (user is null || !passwordHasher.Verify(user, request.Password))
        {
            if (user is not null)
            {
                user.RegisterFailedLogin(now, securityOptions.Value.MaxFailedLoginAttempts, securityOptions.Value.LockoutMinutes);
                await userRepository.SaveChangesAsync(ct);
            }
            return new LoginResultDto(LoginOutcome.InvalidCredentials, null, null, null);
        }

        if (!user.IsActive) return new LoginResultDto(LoginOutcome.AccountDeactivated, null, null, null);
        if (user.IsLockedOut(now)) return new LoginResultDto(LoginOutcome.AccountLocked, null, null, null);

        var maxAgeDays = securityOptions.Value.PasswordMaxAgeDays;
        if (maxAgeDays > 0 && now > user.PasswordChangedAtUtc.AddDays(maxAgeDays))
        {
            var (resetToken, _) = tokenService.IssuePasswordResetChallengeToken(user.Id);
            return new LoginResultDto(LoginOutcome.PasswordExpired, null, null, null, resetToken);
        }

        if (user.MfaEnabled)
        {
            var (challengeToken, _) = tokenService.IssueMfaChallengeToken(user.Id);
            return new LoginResultDto(LoginOutcome.RequiresMfa, null, null, challengeToken);
        }

        return await IssueSuccessAsync(user, ct);
    }

    public async Task<LoginResultDto> CompleteMfaLoginAsync(CompleteMfaLoginRequest request, CancellationToken ct)
    {
        var userId = tokenService.ValidateMfaChallengeToken(request.MfaChallengeToken);
        if (userId is null) return new LoginResultDto(LoginOutcome.InvalidCredentials, null, null, null);

        var user = await userRepository.GetByIdAsync(userId.Value, ct);
        if (user is null || !user.IsActive || user.MfaSecret is null || !totpService.ValidateCode(user.MfaSecret, request.Code, timeProvider.GetUtcNow()))
            return new LoginResultDto(LoginOutcome.InvalidCredentials, null, null, null);

        return await IssueSuccessAsync(user, ct);
    }

    // Companion to LoginAsync's PasswordExpired outcome — the only way an expired-password user
    // can ever set a new password, since ChangePasswordAsync below requires an [Authorize]'d
    // session they can't obtain while expired. Succeeds straight into a full login on completion,
    // same "no dead end" UX as CompleteMfaLoginAsync.
    public async Task<LoginResultDto> CompleteExpiredPasswordChangeAsync(CompleteExpiredPasswordChangeRequest request, CancellationToken ct)
    {
        var userId = tokenService.ValidatePasswordResetChallengeToken(request.PasswordResetChallengeToken);
        if (userId is null) throw new SecurityValidationException(new[] { "This password reset link has expired. Please log in again." });

        var user = await userRepository.GetByIdAsync(userId.Value, ct) ?? throw new UserNotFoundException(userId.Value);
        if (!user.IsActive) throw new SecurityValidationException(new[] { "This account has been deactivated." });

        var errors = passwordPolicyValidator.Validate(request.NewPassword);
        if (errors.Count > 0) throw new SecurityValidationException(errors);

        user.SetPassword(passwordHasher.Hash(user, request.NewPassword), timeProvider.GetUtcNow());
        return await IssueSuccessAsync(user, ct);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(userId, ct) ?? throw new UserNotFoundException(userId);
        if (!passwordHasher.Verify(user, request.CurrentPassword))
            throw new SecurityValidationException(new[] { "Current password is incorrect." });

        var errors = passwordPolicyValidator.Validate(request.NewPassword);
        if (errors.Count > 0) throw new SecurityValidationException(errors);

        user.SetPassword(passwordHasher.Hash(user, request.NewPassword), timeProvider.GetUtcNow());
        await userRepository.SaveChangesAsync(ct);
    }

    public async Task<MfaSetupDto> BeginMfaSetupAsync(Guid userId, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(userId, ct) ?? throw new UserNotFoundException(userId);
        var secret = totpService.GenerateSecret();
        user.BeginMfaSetup(secret);
        await userRepository.SaveChangesAsync(ct);
        return new MfaSetupDto(secret, totpService.GetProvisioningUri(user.Email, secret));
    }

    public async Task ConfirmMfaSetupAsync(Guid userId, ConfirmMfaRequest request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(userId, ct) ?? throw new UserNotFoundException(userId);
        if (user.MfaSecret is null || !totpService.ValidateCode(user.MfaSecret, request.Code, timeProvider.GetUtcNow()))
            throw new SecurityValidationException(new[] { "Invalid verification code." });
        user.ConfirmMfaSetup();
        await userRepository.SaveChangesAsync(ct);
    }

    public async Task DisableMfaAsync(Guid userId, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(userId, ct) ?? throw new UserNotFoundException(userId);
        user.DisableMfa();
        await userRepository.SaveChangesAsync(ct);
    }

    private async Task<LoginResultDto> IssueSuccessAsync(User user, CancellationToken ct)
    {
        user.RegisterSuccessfulLogin();
        await userRepository.SaveChangesAsync(ct);
        var roles = await roleRepository.GetByIdsAsync(await userRepository.GetRoleIdsForUserAsync(user.Id, ct), ct);
        var (accessToken, expiresAt) = tokenService.IssueAccessToken(user, roles);
        return new LoginResultDto(LoginOutcome.Success, accessToken, expiresAt, null);
    }
}
