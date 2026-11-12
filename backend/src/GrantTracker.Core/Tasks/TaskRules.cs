using GrantTracker.Core.Common.Errors;

namespace GrantTracker.Core.Tasks;

public readonly record struct ValidatedTask(string Title, DateOnly DueDate, TaskPriority Priority);

public static class TaskRules
{
    public const int TitleMaxLength = 100;

    public static ValidatedTask Validate(string? title, DateOnly? dueDate, TaskPriority? priority,
        DateOnly applicationDate, DateOnly today, bool enforceDueDate = true)
    {
        var errors = new Dictionary<string, string[]>();
        var codes = new HashSet<string>();
        var trimmed = title?.Trim() ?? "";

        if (trimmed.Length == 0)
        {
            errors["title"] = ["Enter a task title."];
            codes.Add(ErrorCodes.TitleInvalid);
        }
        else if (trimmed.Length > TitleMaxLength)
        {
            errors["title"] = [$"The title must be {TitleMaxLength} characters or fewer."];
            codes.Add(ErrorCodes.TitleInvalid);
        }

        if (dueDate is null)
        {
            errors["dueDate"] = ["Enter a due date."];
            codes.Add(ErrorCodes.ValidationFailed);
        }
        else if (enforceDueDate)
        {
            var dueErrors = new List<string>();
            if (dueDate.Value < today) dueErrors.Add("The due date can't be in the past.");
            if (dueDate.Value < applicationDate) dueErrors.Add("The due date can't be before the grant's application date.");
            if (dueErrors.Count > 0)
            {
                errors["dueDate"] = [.. dueErrors];
                codes.Add(ErrorCodes.DueDateInvalid);
            }
        }

        if (priority is null)
        {
            errors["priority"] = ["Choose a priority."];
            codes.Add(ErrorCodes.ValidationFailed);
        }

        if (errors.Count > 0)
            throw new ValidationException(codes.Count == 1 ? codes.Single() : ErrorCodes.ValidationFailed,
                "The task is invalid.", errors);

        return new ValidatedTask(trimmed, dueDate!.Value, priority!.Value);
    }
}
