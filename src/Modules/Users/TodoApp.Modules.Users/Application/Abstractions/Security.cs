using TodoApp.Modules.Users.Domain;

namespace TodoApp.Modules.Users.Application.Abstractions;

internal interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}

internal interface ITokenProvider
{
    AccessToken Create(User user);
}

internal sealed record AccessToken(string Token, DateTimeOffset ExpiresAtUtc);
