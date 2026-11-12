# Back-end patterns

The back end has three layers: HTTP concerns in `GrantTracker.Api`, business
concepts and use cases in `GrantTracker.Core`, and SQLite persistence in
`GrantTracker.Data`. Keep that dependency direction; consult
[ARCHITECTURE.md](../ARCHITECTURE.md) before changing the structure.

## Add a use case

1. Add a folder under the relevant Core domain (for example
   `Grants/ListGrants/`) with a query or command and an `IRequestHandler`.
2. Keep rules and orchestration in the handler. Load and persist through Core
   ports, pass `IBusinessClock.Today` into date-dependent rules, and do not call
   `SaveChanges`; the command audit pipeline owns the transaction and commit.
3. For commands, set `IAuditContext.SetTarget` when there is a relevant entity and
   `SetOutcome(AuditOutcome.NoChange)` when no state changed.
4. Register the handler explicitly in `CoreServiceCollectionExtensions`.
5. Add a thin controller action that maps HTTP inputs to the request and invokes
   `IMediator.Send<TRequest, TResponse>`. Authenticated endpoints require
   `X-User-Id` by default.
6. Cover pure handler rules in Core tests, persistence/query behavior in Data tests,
   and HTTP status/body/middleware behavior in Api tests. Use Moq only for Core
   ports; use a migrated SQLite in-memory database for persistence tests.

## Idempotent commands

Implement `IIdempotentCommand` on a command and expose its optional
`IdempotencyKey`. Bind `Idempotency-Key` in the controller. The idempotency
behaviour validates and trims it, hashes the request body without the key, stores
the serialized result, and replays matching retries. A different operation or
payload for the same key returns `IDEMPOTENCY_KEY_MISMATCH` (422). Records
currently have no cleanup job; retention/TTL cleanup is a follow-up.

## Rules and errors

Raise typed Core exceptions rather than returning HTTP results from handlers.
Use `ValidationException` for invalid input (400), `NotFoundException` (404), or
`ConflictException` with a stable domain code (409). Add new error codes to
`ErrorCodes` and map them in the Api exception handler only if their status is
not already covered. Unexpected failures remain 500 responses without stack
traces.

## Time and tests

Use `IBusinessClock.UtcNow` for timestamps and `IBusinessClock.Today` for
business-calendar decisions; never use `DateTime.Now`/`DateTime.Today`. Keep
persisted instants UTC and calendar dates as `DateOnly`. Use one database and
one clock per test. Core tests should exercise behavior without EF; Data tests
use SQLite in-memory; Api tests use `WebApplicationFactory` and an isolated
in-memory SQLite database.

## Worked examples

Each excerpt is copied from the file named above it; step V checks them against the source.

### A rule on the entity, with `today` passed in

From [Grant.cs](../../backend/src/GrantTracker.Core/Grants/Grant.cs):

```csharp
    public GrantTask AddTask(Guid taskId, string? title, DateOnly? dueDate, TaskPriority? priority,
        DateOnly today, DateTime nowUtc)
    {
        var valid = TaskRules.Validate(title, dueDate, priority, ApplicationDate, today);
        if (Status == GrantStatus.Closed)
            throw new ConflictException(ErrorCodes.TaskOnClosedGrant, "This grant is closed, so tasks can't be added.");
        var task = new GrantTask(taskId, valid.Title, Id, valid.DueDate, valid.Priority,
            GrantTaskStatus.Pending, null, nowUtc);
        Tasks.Add(task);
        return task;
    }
```

### A query use case

From [ListGrantsHandler.cs](../../backend/src/GrantTracker.Core/Grants/ListGrants/ListGrantsHandler.cs):

```csharp
public sealed class ListGrantsHandler(IGrantRepository grants) : IRequestHandler<ListGrantsQuery, IReadOnlyList<GrantListItemDto>>
{
    public async Task<IReadOnlyList<GrantListItemDto>> Handle(ListGrantsQuery request, CancellationToken cancellationToken)
    {
        GrantStatus? status = null;
        var text = request.Status?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            if (!text.All(char.IsLetter) || !Enum.TryParse<GrantStatus>(text, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                throw new ValidationException(ErrorCodes.ValidationFailed, "The status filter must be Open or Closed.",
                    new Dictionary<string, string[]> { ["status"] = ["Use Open or Closed."] });
            status = parsed;
        }
        var summaries = await grants.ListAsync(status, cancellationToken);
        return summaries.Select(s => GrantListItemDto.From(s)).ToList();
    }
}
```

### A command with an audit target and `NoChange`

From [CompleteTaskHandler.cs](../../backend/src/GrantTracker.Core/Tasks/CompleteTask/CompleteTaskHandler.cs):

```csharp
    public async Task<TaskDto> Handle(CompleteTaskCommand request, CancellationToken cancellationToken)
    {
        audit.SetTarget("Task", request.TaskId);
        var task = await tasks.FindWithGrantAsync(request.TaskId, cancellationToken)
            ?? throw new NotFoundException("Task not found.");
        if (!task.Grant!.CompleteTask(task, clock.UtcNow)) audit.SetOutcome(AuditOutcome.NoChange);
        return TaskDto.From(task, clock.Today);
    }
```

### A port and its EF implementation

The port, from [ITaskRepository.cs](../../backend/src/GrantTracker.Core/Tasks/ITaskRepository.cs):

```csharp
public interface ITaskRepository
{
    void Add(GrantTask task);
    Task<GrantTask?> FindWithGrantAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GrantTask>> ListOverdueAsync(DateOnly today, CancellationToken cancellationToken = default);
}
```

The adapter, from [TaskRepository.cs](../../backend/src/GrantTracker.Data/Repositories/TaskRepository.cs). `Add` is explicit because EF would otherwise treat a new task with a client-set key, reached only through `grant.Tasks`, as an existing row:

```csharp
public sealed class TaskRepository(GrantTrackerDbContext db) : ITaskRepository
{
    public void Add(GrantTask task) => db.Tasks.Add(task);

    public Task<GrantTask?> FindWithGrantAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Tasks.Include(t => t.Grant).SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
```

### A controller action and its request DTO

From [GrantsController.cs](../../backend/src/GrantTracker.Api/Controllers/GrantsController.cs):

```csharp
    [HttpPost("{id:guid}/tasks")]
    public async Task<ActionResult<TaskDto>> AddTask(Guid id, [FromBody] AddTaskRequest request,
        [FromHeader(Name = HeaderNames.IdempotencyKey)] string? idempotencyKey, CancellationToken cancellationToken)
    {
        var task = await mediator.Send<AddTaskCommand, TaskDto>(
            new AddTaskCommand(id, request.Title, request.DueDate, request.Priority, idempotencyKey), cancellationToken);
        Response.Headers.ETag = $"\"{task.Version}\"";
        return StatusCode(StatusCodes.Status201Created, task);
    }
```

From [AddTaskRequest.cs](../../backend/src/GrantTracker.Api/Controllers/AddTaskRequest.cs):

```csharp
public sealed record AddTaskRequest(string? Title, DateOnly? DueDate, TaskPriority? Priority);
```

### Registering a handler

From [CoreServiceCollectionExtensions.cs](../../backend/src/GrantTracker.Api/DependencyInjection/CoreServiceCollectionExtensions.cs):

```csharp
        services.AddScoped<IRequestHandler<AddTaskCommand, TaskDto>, AddTaskHandler>();
```

Repositories are registered in [DataServiceCollectionExtensions.cs](../../backend/src/GrantTracker.Data/DataServiceCollectionExtensions.cs):

```csharp
        services.AddScoped<IGrantRepository, GrantRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
```

### A new error code and its HTTP mapping

Codes live in [ErrorCodes.cs](../../backend/src/GrantTracker.Core/Common/Errors/ErrorCodes.cs):

```csharp
    public const string TaskOnClosedGrant = "BR-1";
    public const string DueDateInvalid = "BR-2";
    public const string TitleInvalid = "BR-3";
    public const string CloseBlockedByHighPriorityTask = "BR-5";
    public const string GrantClosed = "GRANT_CLOSED";
```

Existing exception types already map to statuses in [AppExceptionHandler.cs](../../backend/src/GrantTracker.Api/Errors/AppExceptionHandler.cs), so a rule that throws `ConflictException` needs no mapping change:

```csharp
            ValidationException validation => (400, validation.Code, validation.Message),
            UserNotIdentifiedException identity => (401, identity.Code, identity.Message),
            NotFoundException notFound => (404, notFound.Code, notFound.Message),
            ConflictException conflict => (409, conflict.Code, conflict.Message),
            IdempotencyKeyMismatchException mismatch => (422, mismatch.Code, mismatch.Message),
```

### An idempotent command

From [AddTaskCommand.cs](../../backend/src/GrantTracker.Core/Tasks/AddTask/AddTaskCommand.cs). The key comes from the header, and the behaviour replays a stored result for a matching retry:

```csharp
public sealed record AddTaskCommand(Guid GrantId, string? Title, DateOnly? DueDate, TaskPriority? Priority, string? IdempotencyKey)
    : ICommand<TaskDto>, IIdempotentCommand;
```

### Optimistic concurrency: 412 and 428

A change that must not overwrite someone else's work sends the version it read.
The edit endpoint requires it in `If-Match` and checks it in the handler. From
[VersionConflictException.cs](../../backend/src/GrantTracker.Core/Common/Errors/VersionConflictException.cs):

```csharp
public sealed class VersionConflictException() : AppException(ErrorCodes.VersionConflict,
    "This task was changed by someone else before your edit was saved. Reload to see the latest version, then edit again.");
```

From [EditTaskHandler.cs](../../backend/src/GrantTracker.Core/Tasks/EditTask/EditTaskHandler.cs). A missing version is `428`, a stale one is `412`, and both are checked before the business rules run:

```csharp
        if (request.ExpectedVersion is null) throw new PreconditionRequiredException();
        if (task.Version != request.ExpectedVersion) throw new VersionConflictException();
        task.Grant!.EditTask(task, request.Title, request.DueDate, request.Priority, clock.Today);
```

A race that slips past the check is caught by the unit of work, so the audit behaviour still records `Rejected`. From [EfUnitOfWork.cs](../../backend/src/GrantTracker.Data/EfUnitOfWork.cs):

```csharp
        catch (DbUpdateConcurrencyException)
        {
            throw new VersionConflictException();
        }
```

The Api parses the header in [TasksController.cs](../../backend/src/GrantTracker.Api/Controllers/TasksController.cs). An unreadable value is a `400 MALFORMED_REQUEST`, and an absent one is passed as `null` so the handler answers `428`:

```csharp
    private static int? ParseVersion(string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch)) return null;
        if (int.TryParse(ifMatch.Trim().Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out var version)) return version;
        throw new ValidationException(ErrorCodes.MalformedRequest, "If-Match must contain the task's version.",
            new Dictionary<string, string[]> { ["If-Match"] = ["Send the version as a number, for example \"3\"."] });
    }
```

The mapping for the two new exceptions is in [AppExceptionHandler.cs](../../backend/src/GrantTracker.Api/Errors/AppExceptionHandler.cs):

```csharp
            VersionConflictException versionConflict => (412, versionConflict.Code, versionConflict.Message),
            PreconditionRequiredException preconditionRequired => (428, preconditionRequired.Code, preconditionRequired.Message),
```

The idempotency behaviour runs before the handler, so a retry with the same key replays the first success instead of failing with `412`.

### Tests at each level

Core, with Moq for the port and the fake clock, from [GrantHandlerTests.cs](../../backend/tests/GrantTracker.Core.Tests/Grants/GrantHandlerTests.cs):

```csharp
    [Fact]
    public async Task CloseIsRefusedWhileHighPriorityTaskIsPending()
    {
        var grant = TestGrants.Open();
        grant.AddTask("Verify bank details", TestGrants.Today.AddDays(5), TaskPriority.High);
        var repository = new Mock<IGrantRepository>();
        repository.Setup(x => x.FindWithTasksAsync(grant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(grant);
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new CloseGrantHandler(repository.Object, Clock, new AuditContext()).Handle(new CloseGrantCommand(grant.Id), TestContext.Current.CancellationToken));
        Assert.Equal(ErrorCodes.CloseBlockedByHighPriorityTask, exception.Code);
    }
```

Data, against a migrated SQLite in-memory database, from [TaskSchemaTests.cs](../../backend/tests/GrantTracker.Data.Tests/TaskSchemaTests.cs):

```csharp
    [Fact]
    public async Task HundredCharacterTitleIsAcceptedAndOneHundredAndOneIsRejected()
    {
        await using var database = new SqliteTestDatabase();
        await database.InitialiseAsync();
        await using var context = database.CreateContext();
        var grant = new Grant(Guid.NewGuid(), "GR-8", "Org", new DateOnly(2026, 1, 1), GrantStatus.Open, null);
        context.Grants.Add(grant);
        var hundred = new GrantTask(Guid.NewGuid(), new string('a', 100), grant.Id, new DateOnly(2026, 3, 10),
            TaskPriority.Low, GrantTaskStatus.Pending, null, Now);
        context.Tasks.Add(hundred);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
```

Api, with the factory and its fake clock, from [GrantTrackerApiFactory.cs](../../backend/tests/GrantTracker.Api.Tests/Infrastructure/GrantTrackerApiFactory.cs):

```csharp
        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(GrantTrackerApiFactory).Assembly);
            services.RemoveAll<IBusinessClock>();
            services.AddSingleton<IBusinessClock>(Clock);
        });
```

