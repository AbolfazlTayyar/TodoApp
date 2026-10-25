using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Users.Application.Abstractions;
using TodoApp.Modules.Users.Contracts;
using TodoApp.Modules.Users.Domain;
using TodoApp.Modules.Users.Infrastructure.Database;
using TodoApp.Shared.Abstractions.Events;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Users.Application.RegisterUser;

internal sealed record RegisterUserCommand(string Email, string DisplayName, string Password) : ICommand<Guid>;

internal sealed class RegisterUserCommandHandler(
    UsersDbContext dbContext,
    IPasswordHasher passwordHasher,
    IEventBus eventBus,
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
        await dbContext.SaveChangesAsync(cancellationToken);

        // Users doesn't know (or care) that Todos and Notifications react to this.
        await eventBus.PublishAsync(
            new UserRegisteredIntegrationEvent(user.Id, user.Email, user.DisplayName),
            cancellationToken);

        return user.Id;
    }
}
