using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Common.Time;
using GrantTracker.Core.Auditing;
using GrantTracker.Core.Grants;

namespace GrantTracker.Core.Tasks.AddTask;

public sealed class AddTaskHandler(IGrantRepository grants, ITaskRepository tasks, IBusinessClock clock, IAuditContext audit)
    : IRequestHandler<AddTaskCommand, TaskDto>
{
    public async Task<TaskDto> Handle(AddTaskCommand request, CancellationToken cancellationToken)
    {
        audit.SetTarget("Grant", request.GrantId);
        var grant = await grants.FindAsync(request.GrantId, cancellationToken)
            ?? throw new NotFoundException("Grant not found.");
        var task = grant.AddTask(Guid.NewGuid(), request.Title, request.DueDate, request.Priority, clock.Today, clock.UtcNow);
        tasks.Add(task);
        return TaskDto.From(task, clock.Today);
    }
}
