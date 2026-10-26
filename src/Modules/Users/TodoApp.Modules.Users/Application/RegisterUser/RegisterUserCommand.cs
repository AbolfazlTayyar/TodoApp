using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Users.Application.Abstractions;
using TodoApp.Modules.Users.Contracts;
using TodoApp.Modules.Users.Domain;
using TodoApp.Modules.Users.Infrastructure.Database;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Users.Application.RegisterUser;

internal sealed record RegisterUserCommand(string Email, string DisplayName, string Password) : ICommand<Guid>;

internal sealed class RegisterUserCommandHandler(
    UsersDbContext dbContext,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider) : ICommandHandler<RegisterUserCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(command.Email);

        // The unique index on Email is the real guarantee; this check just gives a friendly error.
        if (await dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return UserErrors.EmailAlreadyInUse;
        }

        var result = User.Register(
            email,
            command.DisplayName,
            passwordHasher.Hash(command.Password),
            timeProvider.GetUtcNow().UtcDateTime);

        if (result.IsFailure)
        {
            return result.Error;
        }

        var user = result.Value;
        dbContext.Users.Add(user);

        // Users doesn't know (or care) that Todos and Notifications react to this. The event goes into the
        // outbox and is saved in the same transaction as the user, so one can never exist without the other.
        dbContext.AddIntegrationEvent(new UserRegisteredIntegrationEvent(user.Id, user.Email, user.DisplayName));
        await dbContext.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
