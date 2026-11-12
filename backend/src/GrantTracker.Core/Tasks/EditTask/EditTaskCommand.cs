using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Idempotency;

namespace GrantTracker.Core.Tasks.EditTask;

public sealed record EditTaskCommand(Guid TaskId, int? ExpectedVersion, string? Title, DateOnly? DueDate,
    TaskPriority? Priority, string? IdempotencyKey) : ICommand<TaskDto>, IIdempotentCommand;
