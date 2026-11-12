using GrantTracker.Core.Common.Mediator;

namespace GrantTracker.Core.Tasks.ListOverdueTasks;

public sealed record ListOverdueTasksQuery() : IQuery<IReadOnlyList<OverdueTaskDto>>;
