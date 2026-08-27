namespace SupportCrm.Application.Security;

using Microsoft.AspNetCore.Identity;
using SupportCrm.Domain.Entities;

// Thin wrapper around ASP.NET Core Identity's standalone PBKDF2 hasher — the package is already
// referenced (SupportCrm.Infrastructure.csproj) for a full Identity/EF Identity-store setup this
// codebase deliberately doesn't use; only the hasher class itself is reused.
public class PasswordHashingService
{
    private readonly PasswordHasher<User> hasher = new();

    public string Hash(User user, string password) => hasher.HashPassword(user, password);

    public bool Verify(User user, string password) =>
        hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
