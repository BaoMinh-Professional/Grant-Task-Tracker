using GrantTracker.Core.Auditing;
using GrantTracker.Core.Common.Behaviours;
using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Common.Time;
using GrantTracker.Core.Users;
using GrantTracker.Core.Users.Login;

namespace GrantTracker.Api.DependencyInjection;

public static class CoreServiceCollectionExtensions
{
    public static IServiceCollection AddGrantTrackerCore(this IServiceCollection services)
    {
        services.AddScoped<IMediator, Mediator>();
        services.AddScoped(typeof(IPipelineBehaviour<,>), typeof(LoggingBehaviour<,>));
        services.AddScoped(typeof(IPipelineBehaviour<,>), typeof(AuditBehaviour<,>));
        services.AddScoped(typeof(IPipelineBehaviour<,>), typeof(IdempotencyBehaviour<,>));
        services.AddScoped<IAuditContext, AuditContext>();
        services.AddScoped<IRequestHandler<LoginQuery, UserDto>, LoginHandler>();
        return services;
    }
}
