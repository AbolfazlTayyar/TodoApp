using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Users.Domain;
using TodoApp.Modules.Users.Infrastructure.Database;
using TodoApp.Shared.Abstractions.Messaging;
using TodoApp.Shared.Abstractions.Results;

namespace TodoApp.Modules.Users.Application.GetUser;

internal sealed record GetUserQuery(Guid UserId) : IQuery<UserResponse>;

internal sealed record UserResponse(Guid Id, string Email, string DisplayName, DateTime CreatedAtUtc);

internal sealed class GetUserQueryHandler(UsersDbContext dbContext) : IQueryHandler<GetUserQuery, UserResponse>
{
    public async Task<Result<UserResponse>> HandleAsync(GetUserQuery query, CancellationToken cancellationToken)
    {
        // Queries project straight to a DTO: only the needed columns are read, and nothing is change-tracked.
        var user = await dbContext.Users
            .Where(u => u.Id == query.UserId)
            .Select(u => new UserResponse(u.Id, u.Email, u.DisplayName, u.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

        return user is null ? UserErrors.NotFound : user;
    }
}
