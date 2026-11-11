using GrantTracker.Core.Auditing;
using GrantTracker.Core.Common.Persistence;
using GrantTracker.Core.Idempotency;
using GrantTracker.Core.Users;
using GrantTracker.Data.Repositories;
using GrantTracker.Data.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GrantTracker.Data;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddGrantTrackerData(this IServiceCollection services, Func<IServiceProvider, string> connectionString)
    {
        services.AddDbContext<GrantTrackerDbContext>((provider, options) => options.UseSqlite(connectionString(provider)));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<DbSeeder>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
