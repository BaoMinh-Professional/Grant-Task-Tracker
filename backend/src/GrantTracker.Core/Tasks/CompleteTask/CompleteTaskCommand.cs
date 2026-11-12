using GrantTracker.Core.Common.Mediator;

namespace GrantTracker.Core.Tasks.CompleteTask;

public sealed record CompleteTaskCommand(Guid TaskId) : ICommand<TaskDto>;
