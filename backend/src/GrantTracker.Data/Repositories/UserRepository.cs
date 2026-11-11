using GrantTracker.Core.Users;
using Microsoft.EntityFrameworkCore;

namespace GrantTracker.Data.Repositories;

public sealed class UserRepository(GrantTrackerDbContext dbContext) : IUserRepository
{
    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalised = EmailAddress.Normalise(email);
        return dbContext.Users.SingleOrDefaultAsync(user => user.Email == normalised, cancellationToken);
    }
}
