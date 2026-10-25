namespace TodoApp.Modules.Users.Contracts;

/// <summary>
/// Synchronous, in-process API that other modules call when they need user data right now.
/// If Users becomes a separate service later, only the implementation changes (to an HTTP/gRPC client).
/// </summary>
public interface IUsersModuleApi
{
    Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record UserDto(Guid Id, string Email, string DisplayName);
