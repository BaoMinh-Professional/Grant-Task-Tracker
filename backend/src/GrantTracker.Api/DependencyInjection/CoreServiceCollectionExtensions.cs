using GrantTracker.Api.Idempotency;
using GrantTracker.Core.Auditing;
using GrantTracker.Core.Common.Behaviours;
using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Common.Time;
using GrantTracker.Core.Grants;
using GrantTracker.Core.Grants.CloseGrant;
using GrantTracker.Core.Grants.GetGrant;
using GrantTracker.Core.Grants.ListGrants;
using GrantTracker.Core.Idempotency;
using GrantTracker.Core.Tasks;
using GrantTracker.Core.Tasks.AddTask;
using GrantTracker.Core.Tasks.CompleteTask;
using GrantTracker.Core.Tasks.EditTask;
using GrantTracker.Core.Tasks.ListOverdueTasks;
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
        services.AddScoped<IdempotencyCleanup>();
        services.AddHostedService<IdempotencyCleanupService>();
        services.AddScoped<IRequestHandler<LoginQuery, UserDto>, LoginHandler>();
        services.AddScoped<IRequestHandler<ListGrantsQuery, IReadOnlyList<GrantListItemDto>>, ListGrantsHandler>();
        services.AddScoped<IRequestHandler<GetGrantQuery, GrantDetailDto>, GetGrantHandler>();
        services.AddScoped<IRequestHandler<ListOverdueTasksQuery, IReadOnlyList<OverdueTaskDto>>, ListOverdueTasksHandler>();
        services.AddScoped<IRequestHandler<AddTaskCommand, TaskDto>, AddTaskHandler>();
        services.AddScoped<IRequestHandler<CompleteTaskCommand, TaskDto>, CompleteTaskHandler>();
        services.AddScoped<IRequestHandler<CloseGrantCommand, GrantListItemDto>, CloseGrantHandler>();
        services.AddScoped<IRequestHandler<EditTaskCommand, TaskDto>, EditTaskHandler>();
        return services;
    }
}
