using GrantTracker.Core.Auditing;
using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Common.Time;

namespace GrantTracker.Core.Tasks.CompleteTask;

public sealed class CompleteTaskHandler(ITaskRepository tasks, IBusinessClock clock, IAuditContext audit)
    : IRequestHandler<CompleteTaskCommand, TaskDto>
{
    public async Task<TaskDto> Handle(CompleteTaskCommand request, CancellationToken cancellationToken)
    {
        audit.SetTarget("Task", request.TaskId);
        var task = await tasks.FindWithGrantAsync(request.TaskId, cancellationToken)
            ?? throw new NotFoundException("Task not found.");
        if (!task.Grant!.CompleteTask(task, clock.UtcNow)) audit.SetOutcome(AuditOutcome.NoChange);
        return TaskDto.From(task, clock.Today);
    }
}
