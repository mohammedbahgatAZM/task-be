namespace SupportCrm.Application.Security;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class UserManagementService(
    IUserRepository userRepository, IRoleRepository roleRepository,
    PasswordHashingService passwordHasher, PasswordPolicyValidator passwordPolicyValidator, TimeProvider timeProvider)
{
    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await userRepository.GetByEmailAsync(email, ct) is not null) throw new DuplicateEmailException(email);

        var errors = passwordPolicyValidator.Validate(request.InitialPassword);
        if (errors.Count > 0) throw new SecurityValidationException(errors);

        var now = timeProvider.GetUtcNow();
        var user = new User(email, "placeholder", now); // real hash computed below, once the User instance exists
        user.SetPassword(passwordHasher.Hash(user, request.InitialPassword), now);

        await userRepository.AddAsync(user, ct);
        await userRepository.SaveChangesAsync(ct);
        await userRepository.SetUserRolesAsync(user.Id, request.RoleIds, ct);
        await userRepository.SaveChangesAsync(ct);

        return await ToDtoAsync(user, ct);
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct)
    {
        var result = new List<UserDto>();
        foreach (var user in await userRepository.GetAllAsync(ct)) result.Add(await ToDtoAsync(user, ct));
        return result;
    }

    public async Task DeactivateAsync(Guid userId, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(userId, ct) ?? throw new UserNotFoundException(userId);
        user.Deactivate();
        await userRepository.SaveChangesAsync(ct);
    }

    public async Task ActivateAsync(Guid userId, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(userId, ct) ?? throw new UserNotFoundException(userId);
        user.Activate();
        await userRepository.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid userId, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(userId, ct) ?? throw new UserNotFoundException(userId);
        await userRepository.DeleteAsync(user, ct);
        await userRepository.SaveChangesAsync(ct);
    }

    public async Task SetRolesAsync(Guid userId, SetUserRolesRequest request, CancellationToken ct)
    {
        _ = await userRepository.GetByIdAsync(userId, ct) ?? throw new UserNotFoundException(userId);
        await userRepository.SetUserRolesAsync(userId, request.RoleIds, ct);
        await userRepository.SaveChangesAsync(ct);
    }

    private async Task<UserDto> ToDtoAsync(User user, CancellationToken ct)
    {
        var roles = await roleRepository.GetByIdsAsync(await userRepository.GetRoleIdsForUserAsync(user.Id, ct), ct);
        return new UserDto(user.Id, user.Email, user.IsActive, user.MfaEnabled, user.PasswordChangedAtUtc, user.CreatedAtUtc, roles.Select(r => r.Name).ToList());
    }
}
