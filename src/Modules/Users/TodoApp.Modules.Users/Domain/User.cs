using TodoApp.Shared.Abstractions.Domain;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Users.Domain;

// Rich domain model: private setters and a factory method, so a User can never exist in an invalid state.
internal sealed class User : Entity
{
    public const int EmailMaxLength = 256;
    public const int DisplayNameMaxLength = 100;

    private User(Guid id, string email, string displayName, string passwordHash, DateTime createdAtUtc)
        : base(id)
    {
        Email = email;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        CreatedAtUtc = createdAtUtc;
    }

    private User()
    {
    }

    public string Email { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static Result<User> Register(string email, string displayName, string passwordHash, DateTime utcNow)
    {
        displayName = displayName.Trim();
        if (displayName.Length is 0 or > DisplayNameMaxLength)
        {
            return UserErrors.InvalidDisplayName;
        }

        // IDs are generated in code (no database round trip), so the entity is complete before it's saved.
        return new User(Guid.CreateVersion7(), NormalizeEmail(email), displayName, passwordHash, utcNow);
    }
}
