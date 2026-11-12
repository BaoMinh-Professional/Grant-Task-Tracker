using System.Globalization;
using GrantTracker.Api.Identity;
using GrantTracker.Core.Common.Errors;
using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Tasks;
using GrantTracker.Core.Tasks.CompleteTask;
using GrantTracker.Core.Tasks.EditTask;
using GrantTracker.Core.Tasks.ListOverdueTasks;
using Microsoft.AspNetCore.Mvc;

namespace GrantTracker.Api.Controllers;

[ApiController]
[Route("tasks")]
public sealed class TasksController(IMediator mediator) : ControllerBase
{
    [HttpPut("{id:guid}/complete")]
    public async Task<ActionResult<TaskDto>> Complete(Guid id, CancellationToken cancellationToken)
    {
        var task = await mediator.Send<CompleteTaskCommand, TaskDto>(new CompleteTaskCommand(id), cancellationToken);
        Response.Headers.ETag = $"\"{task.Version}\"";
        return Ok(task);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TaskDto>> Edit(Guid id, [FromBody] EditTaskRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = HeaderNames.IdempotencyKey)] string? idempotencyKey, CancellationToken cancellationToken)
    {
        var task = await mediator.Send<EditTaskCommand, TaskDto>(
            new EditTaskCommand(id, ParseVersion(ifMatch), request.Title, request.DueDate, request.Priority, idempotencyKey),
            cancellationToken);
        Response.Headers.ETag = $"\"{task.Version}\"";
        return Ok(task);
    }

    [HttpGet("overdue")]
    public async Task<ActionResult<IReadOnlyList<OverdueTaskDto>>> Overdue(CancellationToken cancellationToken) =>
        Ok(await mediator.Send<ListOverdueTasksQuery, IReadOnlyList<OverdueTaskDto>>(new ListOverdueTasksQuery(), cancellationToken));

    private static int? ParseVersion(string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch)) return null;
        if (int.TryParse(ifMatch.Trim().Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out var version)) return version;
        throw new ValidationException(ErrorCodes.MalformedRequest, "If-Match must contain the task's version.",
            new Dictionary<string, string[]> { ["If-Match"] = ["Send the version as a number, for example \"3\"."] });
    }
}
