using GrantTracker.Core.Tasks;

namespace GrantTracker.Api.Controllers;

public sealed record EditTaskRequest(string? Title, DateOnly? DueDate, TaskPriority? Priority);
