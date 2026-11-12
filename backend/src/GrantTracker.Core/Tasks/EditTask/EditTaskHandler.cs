using GrantTracker.Core.Auditing;
using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Common.Time;

namespace GrantTracker.Core.Tasks.EditTask;

public sealed class EditTaskHandler(ITaskRepository tasks, IBusinessClock clock, IAuditContext audit)
    : IRequestHandler<EditTaskCommand, TaskDto>
{
    public async Task<TaskDto> Handle(EditTaskCommand request, CancellationToken cancellationToken)
    {
        audit.SetTarget("Task", request.TaskId);
        var task = await tasks.FindWithGrantAsync(request.TaskId, cancellationToken)
            ?? throw new NotFoundException("Task not found.");
        if (request.ExpectedVersion is null) throw new PreconditionRequiredException();
        if (task.Version != request.ExpectedVersion) throw new VersionConflictException();
        task.Grant!.EditTask(task, request.Title, request.DueDate, request.Priority, clock.Today);
        return TaskDto.From(task, clock.Today);
    }
}
