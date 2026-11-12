using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Idempotency;

namespace GrantTracker.Core.Tasks.AddTask;

public sealed record AddTaskCommand(Guid GrantId, string? Title, DateOnly? DueDate, TaskPriority? Priority, string? IdempotencyKey)
    : ICommand<TaskDto>, IIdempotentCommand;
