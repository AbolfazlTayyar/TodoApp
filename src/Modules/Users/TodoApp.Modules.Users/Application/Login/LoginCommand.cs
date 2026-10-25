using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Users.Application.Abstractions;
using TodoApp.Modules.Users.Domain;
using TodoApp.Modules.Users.Infrastructure.Database;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Users.Application.Login;

internal sealed record LoginCommand(string Email, string Password) : ICommand<AccessToken>;

internal sealed class LoginCommandHandler(
    UsersDbContext dbContext,
    IPasswordHasher passwordHasher,
    ITokenProvider tokenProvider) : ICommandHandler<LoginCommand, AccessToken>
{
    public async Task<Result<AccessToken>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(command.Email);

        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || !passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            return UserErrors.InvalidCredentials;
        }

        return tokenProvider.Create(user);
    }
}
