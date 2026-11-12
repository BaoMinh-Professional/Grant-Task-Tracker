using GrantTracker.Core.Tasks;

namespace GrantTracker.Api.Controllers;

public sealed record AddTaskRequest(string? Title, DateOnly? DueDate, TaskPriority? Priority);
