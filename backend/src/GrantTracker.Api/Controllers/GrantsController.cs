using GrantTracker.Api.Identity;
using GrantTracker.Core.Common.Mediator;
using GrantTracker.Core.Grants;
using GrantTracker.Core.Grants.CloseGrant;
using GrantTracker.Core.Grants.GetGrant;
using GrantTracker.Core.Grants.ListGrants;
using GrantTracker.Core.Tasks;
using GrantTracker.Core.Tasks.AddTask;
using Microsoft.AspNetCore.Mvc;

namespace GrantTracker.Api.Controllers;

[ApiController]
[Route("grants")]
public sealed class GrantsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GrantListItemDto>>> List([FromQuery] string? status, CancellationToken cancellationToken) =>
        Ok(await mediator.Send<ListGrantsQuery, IReadOnlyList<GrantListItemDto>>(new ListGrantsQuery(status), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GrantDetailDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send<GetGrantQuery, GrantDetailDto>(new GetGrantQuery(id), cancellationToken));

    [HttpPost("{id:guid}/tasks")]
    public async Task<ActionResult<TaskDto>> AddTask(Guid id, [FromBody] AddTaskRequest request,
        [FromHeader(Name = HeaderNames.IdempotencyKey)] string? idempotencyKey, CancellationToken cancellationToken)
    {
        var task = await mediator.Send<AddTaskCommand, TaskDto>(
            new AddTaskCommand(id, request.Title, request.DueDate, request.Priority, idempotencyKey), cancellationToken);
        Response.Headers.ETag = $"\"{task.Version}\"";
        return StatusCode(StatusCodes.Status201Created, task);
    }

    [HttpPut("{id:guid}/close")]
    public async Task<ActionResult<GrantListItemDto>> Close(Guid id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send<CloseGrantCommand, GrantListItemDto>(new CloseGrantCommand(id), cancellationToken));
}
