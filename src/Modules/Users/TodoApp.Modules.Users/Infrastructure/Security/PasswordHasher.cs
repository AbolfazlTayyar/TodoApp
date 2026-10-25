using Microsoft.AspNetCore.Identity;
using TodoApp.Modules.Users.Application.Abstractions;

namespace TodoApp.Modules.Users.Infrastructure.Security;

// Never write your own crypto. This wraps ASP.NET Core Identity's PBKDF2 hasher (salted, 100k+ iterations).
internal sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();
    private readonly object _unusedUser = new();

    public string Hash(string password) => _hasher.HashPassword(_unusedUser, password);

    public bool Verify(string password, string passwordHash) =>
        _hasher.VerifyHashedPassword(_unusedUser, passwordHash, password) != PasswordVerificationResult.Failed;
}
