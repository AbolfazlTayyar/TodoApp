using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Users.Domain;

internal static class UserErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Users.NotFound", "The user was not found.");

    public static readonly Error EmailAlreadyInUse =
        Error.Conflict("Users.EmailAlreadyInUse", "A user with this email already exists.");

    // Deliberately vague: don't reveal whether the email or the password was wrong.
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Users.InvalidCredentials", "The email or password is incorrect.");

    public static readonly Error InvalidDisplayName =
        Error.Validation("Users.InvalidDisplayName", $"Display name must be 1 to {User.DisplayNameMaxLength} characters.");
}
