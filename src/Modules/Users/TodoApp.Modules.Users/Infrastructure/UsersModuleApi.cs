using Microsoft.EntityFrameworkCore;
using TodoApp.Modules.Users.Contracts;
using TodoApp.Modules.Users.Infrastructure.Database;

namespace TodoApp.Modules.Users.Infrastructure;

internal sealed class UsersModuleApi(UsersDbContext dbContext) : IUsersModuleApi
{
    public Task<UserDto?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => new UserDto(u.Id, u.Email, u.DisplayName))
            .SingleOrDefaultAsync(cancellationToken);
}
