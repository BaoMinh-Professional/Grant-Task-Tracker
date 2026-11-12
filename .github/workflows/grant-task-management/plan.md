# Grant and task management: implementation plan

> **Stage:** 2, implementation plan (detailed, written so a small model can follow it
> step by step). **Status:** ✅ **Approved by the developer on 2026-11-12.** Implementation
> may start with step B1 ([prompt-style.md](../../../prompt-style.md)). The agent makes no
> commits or pull requests.
> **Source of decisions:** [ideal.md](./ideal.md) (GREEN, 2026-11-12, Q1 to Q17). Decisions
> there are carried in as-is and not re-argued here.
> **Open questions:** OQ-1 to OQ-8 (section 10) come from the screen-versus-API review and
> are all **decided** (developer, 2026-11-12). Answers that change ideal.md sections 3 to 5
> (the grant detail shape with `canClose`, BR-2 as a field error, `status` validation,
> staleness) are copied into ideal.md as the first action of implementation, because "changes to a
> decision must be recorded in that file".

---

## 0. How to use this plan (read first)

1. Work **top to bottom**, one step at a time. Every step ends with a **Verify** command
   and a **Done when** line. Do not start the next step until both pass.
2. **Every step leaves the repository building and every test passing.** If a step breaks
   an existing test, fix it inside that same step (the step says which tests change).
3. Tick the checkbox of each item as you finish it. The checkboxes are the single source
   of progress.
4. Code blocks are the intended design. If one does not compile, fix it minimally, keep the
   behaviour, and write one line under the step saying what you changed.
5. **Do not**: commit or push (the developer commits); add any dependency not listed in
   step F1; edit `AGENTS.md`; edit any file under `docs/` except in the documentation
   steps (D1 to D3, and E4 if phase 2 is built); use `DateTime.Now` or `DateTime.Today` anywhere.
6. Shell: PowerShell on Windows. Use `npm.cmd` (not `npm`; script execution is blocked).

### Commands

| What | Command (from the folder shown) |
| --- | --- |
| Build the back end | `cd D:\Project\backend; dotnet build` |
| Test the back end (all projects, about 30 s) | `cd D:\Project\backend; dotnet test` |
| Add an EF migration | `cd D:\Project\backend; dotnet ef migrations add <Name> --project src/GrantTracker.Data --startup-project src/GrantTracker.Api` |
| Run the API | `cd D:\Project\backend; dotnet run --project src/GrantTracker.Api --launch-profile http` (listens on `http://localhost:5080`) |
| Front-end tests | `cd D:\Project\frontend; npm.cmd run test` |
| Front-end type check and build | `cd D:\Project\frontend; npm.cmd run build` |
| Front-end lint | `cd D:\Project\frontend; npm.cmd run lint` |
| Run the front end | `cd D:\Project\frontend; npm.cmd run dev` (`http://localhost:5173`) |

Baseline (verified before writing this plan): `dotnet test` passes with 11 tests, and
`dotnet ef migrations list` shows one migration, `20261111112413_InitialCreate`.

### Code conventions to copy from the existing code

- C#: file-scoped `namespace`, `sealed` classes, primary constructors, records for DTOs and
  commands, nullable enabled (warnings as errors for nullable), no comments unless the code
  needs one. Handlers live in one folder per use case, e.g. `Core/Grants/ListGrants/`.
- Tests: xUnit v3 (`[Fact]`, `Assert`, `TestContext.Current.CancellationToken` on every call
  that takes a token). Moq **only** in `Core.Tests` and only for Core ports. Data tests use a
  migrated SQLite in-memory database (`SqliteTestDatabase`). Api tests use
  `GrantTrackerApiFactory`. A test never uses the real date: pass a fake `today`.
- TypeScript: functional components, named exports, `import type` for types, no `any`,
  exact dependency versions, no raw colours in CSS (use tokens from `theme.css`).
- JSON from the API is camelCase; enums are strings (`"High"`); dates are `yyyy-MM-dd`;
  moments are UTC ISO-8601.

---

## 1. Goal and scope

### 1.1 Goal

A grants officer opens one page, finds a grant, adds and completes its tasks, filters
them and closes the grant. The server enforces BR-1 to BR-6; the screen only reflects
and explains them.

### 1.2 In scope

- **Phase 1 (core):** back end for list grants, get grant, add task, complete task,
  close grant and list overdue tasks; BR-1 to BR-6; one migration (title 100, `Version`);
  seed from the PDF; the front-end grants list and grant tasks view with Add task,
  Complete and Close; tests at every level; matching documentation.
- **Phase 2 (extra, cuttable):** Edit task (title, priority, due date) with optimistic
  concurrency (Q4, Q17) and an idempotency key (Q14).

### 1.3 Out of scope

- Assignment of any kind (Q2, Q12): no assign, unassign, grant team, Assigned column,
  `GET /users`, `ASSIGNEE_NOT_ON_GRANT`. The assignment tables stay, unused (Q16).
- Operations needs OP-1 to OP-6 (separate session), a screen for `GET /tasks/overdue`,
  a router, pagination (OQ-8), server-side task filtering, commits and pull requests.

---

## 2. Screen versus API review (index)

The screens in ideal.md section 3 were compared with the endpoints in section 5. Everything
the screens need is covered except the gaps below; each is an open question in section 10
(all decided) and is built into the contract in section 3.

| Question | Finding in one line | Decision |
| --- | --- | --- |
| OQ-1 | The Close button on the tasks view had no data source | A: list item and detail both return `pendingHighTasks` and `canClose` |
| OQ-2 | Grant-detail fields were only implied | A: explicit, self-contained detail, fetched fresh |
| OQ-3 | BR-2 shown beside the field or in the dialog; field names | A: `400` field errors keyed `dueDate` and `title`, beside the field |
| OQ-4 | Invalid `?status=` | A: `400 VALIDATION_FAILED`, case-insensitive match |
| OQ-5 | Stale counts, `canClose` and `today` | A: refetch on open and on focus, no polling, limit documented |
| OQ-6 | Non-inline error codes | A: messages added; the 422 shows in the dialog |
| OQ-7 | 412 and 428 cannot be produced yet | A: two new Core exception types (phase 2) |
| OQ-8 | Unpaged responses | A: no pagination now |

---

## 3. The contract (what the back end and the front end agree on)

### 3.1 Endpoints

All endpoints need the `X-User-Id` header (existing middleware). Unknown ids and malformed
GUID routes return `404 NOT_FOUND`.

| Method and path | Request | Success | Errors |
| --- | --- | --- | --- |
| `GET /grants?status=Open\|Closed` | Optional `status`, case-insensitive; omitted or empty means all | `200` array of **grant list item** | `400 VALIDATION_FAILED` (`errors.status`) for any other value, including numbers |
| `GET /grants/{id}` | Path id | `200` **grant detail** | `404 NOT_FOUND` |
| `POST /grants/{id}/tasks` | Header `Idempotency-Key` (required); JSON body `{ "title", "dueDate", "priority" }` | `201` **task** + `ETag: "<version>"` | `404`, `400` (BR-2, BR-3, `VALIDATION_FAILED`, `IDEMPOTENCY_KEY_REQUIRED`), `409 BR-1`, `422 IDEMPOTENCY_KEY_MISMATCH` |
| `PUT /tasks/{id}/complete` | No body | `200` **task** + `ETag` (a repeat returns the same task, completion date unchanged) | `404`, `409 GRANT_CLOSED` (only when the task is still pending) |
| `PUT /grants/{id}/close` | No body | `200` **grant list item** (a repeat returns it unchanged) | `404`, `409 BR-5` |
| `GET /tasks/overdue` | None | `200` array of **overdue task**, most overdue first | none |
| `PUT /tasks/{id}` (phase 2) | Headers `If-Match: "<version>"` and `Idempotency-Key`; body `{ "title", "dueDate", "priority" }` | `200` **task** + `ETag` | `404`, `400`, `409 GRANT_CLOSED`, `409 TASK_COMPLETED`, `412 VERSION_CONFLICT`, `428 PRECONDITION_REQUIRED`, `422` |

### 3.2 Response shapes

```jsonc
// grant list item (GET /grants, PUT /grants/{id}/close)
{ "id": "00000000-0000-0000-0000-000000000101", "referenceNumber": "GR-100001",
  "organisationName": "Riverside Community Garden Inc.", "applicationDate": "2026-01-09",
  "status": "Open", "totalTasks": 3, "completedTasks": 1, "pendingHighTasks": 1, "canClose": false }

// grant detail (GET /grants/{id})
{ "id": "...", "referenceNumber": "GR-100001", "organisationName": "...", "applicationDate": "2026-01-09",
  "status": "Open", "today": "2026-03-10", "pendingHighTasks": 1, "canClose": false,
  "tasks": [ /* task[] ordered by dueDate then title */ ] }

// task
{ "id": "...", "grantId": "...", "title": "Verify bank details", "priority": "High",
  "dueDate": "2026-03-07", "status": "Pending", "completedAtUtc": null,
  "isOverdue": true, "daysOverdue": 3, "version": 1 }

// overdue task (GET /tasks/overdue); grantStatus is included because of Q15
{ "id": "...", "grantId": "...", "grantReferenceNumber": "GR-100001", "organisationName": "...",
  "grantStatus": "Open", "title": "...", "priority": "High", "dueDate": "2026-03-07",
  "daysOverdue": 3, "version": 1 }
```

`today` is the **business clock's** date (`Asia/Singapore`), never the browser's.
`canClose` is true only when the grant is `Open` and `pendingHighTasks` is 0, and comes from
the single Core rule `GrantRules.CanClose` (OQ-1).

### 3.3 Error codes and messages

Every error is `application/problem+json` with `status`, `title`, `code`, `traceId`; 400
validation errors also have `errors`. The `title` is written for officers and the front end
shows it inline.

| Code | HTTP | Title (exact text) | Notes |
| --- | --- | --- | --- |
| `BR-1` | 409 | This grant is closed, so tasks can't be added. | Add task to a closed grant |
| `BR-2` | 400 | The task is invalid. | `errors.dueDate`: "The due date can't be in the past." and/or "The due date can't be before the grant's application date." |
| `BR-3` | 400 | The task is invalid. | `errors.title`: "Enter a task title." or "The title must be 100 characters or fewer." |
| `VALIDATION_FAILED` | 400 | The task is invalid. | Used when errors of different kinds occur together, or a due date or priority is missing: `errors.dueDate`: "Enter a due date."; `errors.priority`: "Choose a priority." |
| `BR-5` | 409 | This grant can't be closed while it has High priority tasks pending. | Close with a pending High task |
| `GRANT_CLOSED` | 409 | This grant is closed. | Complete (or edit) a pending task on a closed grant |
| `NOT_FOUND` | 404 | Grant not found. / Task not found. | |
| `TASK_COMPLETED` (phase 2) | 409 | A completed task can't be edited. | |
| `VERSION_CONFLICT` (phase 2) | 412 | This task was changed by someone else before your edit was saved. Reload to see the latest version, then edit again. | OQ-7 wording |
| `PRECONDITION_REQUIRED` (phase 2) | 428 | Send the task's current version in the If-Match header. | |

Rule order for **Add task** (Q7): 404 grant, then input (BR-3, BR-2, required fields, all
errors together, one response), then BR-1. BR-4: completing an already completed task is
`200` and audited `NoChange`, even if the grant has since closed. BR-5: only a pending
**High** task blocks closing; closing a closed grant is `200` `NoChange`. BR-6: overdue means
`Pending` and `dueDate < today`; `daysOverdue = today - dueDate` in days; completed tasks are
never overdue (including tasks of closed grants, which stay on the overdue list, Q15).

### 3.4 Headers and CORS

`ETag` is exposed to the browser (`WithExposedHeaders("ETag")`, step B6). Phase 2 also allows
the `If-Match` request header.

---

## 4. Confirmed decisions (from ideal.md, no re-litigating)

Q1 only pending High blocks Close. Q2/Q12 no assignment. Q3 "Overdue by N days" in yellow.
Q4 Edit is last and cuttable. Q5 closed grant is read-only. Q6 repeats return `200`.
Q7 check order. Q8 no router. Q9 migrate title to 100 with a check constraint, applied by
`DatabaseInitializer`. Q10 seed replaced. Q11 `Asia/Singapore` committed. Q13 docs and
test dependencies approved. Q14 keys on Add and Edit only. Q15 overdue list includes closed
grants' pending tasks. Q16 keep assignment tables, comment them. Q17 optimistic concurrency.
OQ-1 to OQ-8 as in section 10.

---

## 5. Back-end steps (phase 1)

Files below are relative to `D:\Project\backend\`. `Core` means `src\GrantTracker.Core`,
`Data` means `src\GrantTracker.Data`, `Api` means `src\GrantTracker.Api`.

### Step B1: Core rules and error codes (pure C#, no database)

**Goal:** BR-1, BR-2, BR-3, BR-4, BR-5, BR-6 and the closed-grant rule exist as plain methods
that tests can call with a fake `today`.

- [x] **B1.1** Edit `Core\Common\Errors\ErrorCodes.cs`: add

  ```csharp
  public const string TaskOnClosedGrant = "BR-1";
  public const string DueDateInvalid = "BR-2";
  public const string TitleInvalid = "BR-3";
  public const string CloseBlockedByHighPriorityTask = "BR-5";
  public const string GrantClosed = "GRANT_CLOSED";
  ```

- [x] **B1.2** Create `Core\Tasks\TaskRules.cs` (validation shared by Add and, in phase 2, Edit):

  ```csharp
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
  ```

  Error keys are camelCase (`dueDate`); the front end lowercases them (`duedate`).

- [x] **B1.3** Edit `Core\Tasks\GrantTask.cs`: add (keep the existing constructor unchanged)

  ```csharp
  public int Version { get; private set; } = 1;

  public bool Complete(DateTime completedAtUtc)
  {
      if (Status == GrantTaskStatus.Completed) return false;
      Status = GrantTaskStatus.Completed;
      CompletedAtUtc = completedAtUtc;
      Version++;
      return true;
  }

  public bool IsOverdue(DateOnly today) => Status == GrantTaskStatus.Pending && DueDate < today;

  public int DaysOverdue(DateOnly today) => IsOverdue(today) ? today.DayNumber - DueDate.DayNumber : 0;
  ```

  `Version` is 1 on creation and increases on every change (Q17). Callers must complete a
  task through `Grant.CompleteTask` so the closed-grant rule applies.

- [x] **B1.4** Create `Core\Grants\GrantRules.cs`:

  ```csharp
  namespace GrantTracker.Core.Grants;

  public static class GrantRules
  {
      public static bool CanClose(GrantStatus status, int pendingHighTasks) =>
          status == GrantStatus.Open && pendingHighTasks == 0;
  }
  ```

- [x] **B1.5** Edit `Core\Grants\Grant.cs` (add `using GrantTracker.Core.Common.Errors;`):

  ```csharp
  public int PendingHighTaskCount => Tasks.Count(t =>
      t.Status == GrantTaskStatus.Pending && t.Priority == TaskPriority.High);

  public bool CanClose => GrantRules.CanClose(Status, PendingHighTaskCount);

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

  public bool CompleteTask(GrantTask task, DateTime nowUtc)
  {
      if (task.GrantId != Id) throw new ArgumentException("The task belongs to another grant.", nameof(task));
      if (task.Status == GrantTaskStatus.Completed) return false;
      if (Status == GrantStatus.Closed)
          throw new ConflictException(ErrorCodes.GrantClosed, "This grant is closed.");
      return task.Complete(nowUtc);
  }

  public bool Close(DateTime nowUtc)
  {
      if (Status == GrantStatus.Closed) return false;
      if (PendingHighTaskCount > 0)
          throw new ConflictException(ErrorCodes.CloseBlockedByHighPriorityTask,
              "This grant can't be closed while it has High priority tasks pending.");
      Status = GrantStatus.Closed;
      ClosedAtUtc = nowUtc;
      return true;
  }
  ```

  Order inside `AddTask` is the Q7 order (input first, then BR-1). `PendingHighTaskCount` and
  `Close` need `Tasks` loaded; the repository does that (step B5).

- [x] **B1.6** Comment `Core\Assignments\GrantAssignment.cs` and `TaskAssignment.cs` (and, in
  step B2, their configurations) with one line each: "Present but not used by the grant and
  task features (assignment is out of scope)." (Q16)

- [x] **B1.7** Core tests (new files; `today` is `new DateOnly(2026, 3, 10)`, `now` is
  `new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc)`):

  `Core.Tests\Tasks\TaskRulesTests.cs`

  | Case | Input | Expected |
  | --- | --- | --- |
  | Valid, trimmed | title `"  Check  "`, due = today, High, application 2026-02-01 | title `"Check"`, due unchanged |
  | BR-2 boundary today | due = today | passes |
  | BR-2 past | due = 2026-03-09 | `ValidationException`, code `BR-2`, `Errors["dueDate"]` = past message |
  | BR-2 application boundary | application 2026-03-15, due 2026-03-15 | passes |
  | BR-2 before application | application 2026-03-15, due 2026-03-12 (not in the past) | code `BR-2`, application-date message only |
  | BR-2 both reasons | application 2026-03-15, due 2026-03-01 | code `BR-2`, both messages in order |
  | BR-3 blank | title `null`, `""`, `"   "` | code `BR-3`, `Errors["title"]` = "Enter a task title." |
  | BR-3 length | 100 chars passes; 101 chars fails | code `BR-3`, "…100 characters or fewer." |
  | Mixed | blank title and past due date | code `VALIDATION_FAILED`, both keys present |
  | Required fields | due `null`; priority `null` | code `VALIDATION_FAILED`; keys `dueDate`, `priority` |
  | Edit mode | `enforceDueDate: false`, due in the past | passes |

  `Core.Tests\Tasks\GrantTaskTests.cs`: first `Complete(now)` returns `true`, sets
  `Completed`, `CompletedAtUtc == now`, `Version == 2`; second call with a later time returns
  `false`, date and version unchanged (BR-4); `IsOverdue`/`DaysOverdue`: due yesterday pending
  → `true`, 1; due today → `false`, 0; due 3 days ago → 3; completed task past due → `false`, 0.

  `Core.Tests\Grants\GrantTests.cs`: open grant `AddTask` valid → task is pending, `Version` 1,
  present in `Tasks`; closed grant + valid input → `ConflictException` `BR-1`; closed grant +
  invalid input → `ValidationException` (proves Q7 order); `Close` with a pending High →
  `ConflictException` `BR-5`; with only pending Medium/Low → returns `true`, `Closed`,
  `ClosedAtUtc == now`; with a completed High → closes; already closed → `false`, `ClosedAtUtc`
  unchanged; `CompleteTask` on a closed grant with a pending task → `GRANT_CLOSED`; on an already
  completed task of a closed grant → `false`; `CanClose` truth table (Open/0 → true; Open/1 →
  false; Closed/0 → false) through `GrantRules.CanClose`.

  Build tasks with `new GrantTask(Guid.NewGuid(), "t", grant.Id, due, priority, status, completedAt, now)`
  and `grant.Tasks.Add(task)`.

**Verify:** `cd D:\Project\backend; dotnet test`
**Done when:** all existing tests and the new Core tests pass.

### Step B2: Schema, migration and constraint tests

**Goal:** the database mirrors BR-3 (100 characters) and has the `Version` concurrency token (Q9, Q17).

- [x] **B2.1** Edit `Data\Configurations\GrantTaskConfiguration.cs`:
  - Change the `ToTable` lambda to add a second check constraint:
    `table.HasCheckConstraint("CK_Tasks_Title_Length", "length(\"Title\") <= 100");` (keep the existing one).
  - `builder.Property(x => x.Title).HasMaxLength(100).IsRequired();`
  - `builder.Property(x => x.Version).IsConcurrencyToken();`
  - Add the Q16 one-line comment to `GrantAssignmentConfiguration.cs` and `TaskAssignmentConfiguration.cs`.
- [x] **B2.2** Generate the migration:
  `cd D:\Project\backend; dotnet ef migrations add TaskTitleLengthAndVersion --project src/GrantTracker.Data --startup-project src/GrantTracker.Api`.
  Then open the generated `*_TaskTitleLengthAndVersion.cs` and make sure the `AddColumn` for
  `Version` has `defaultValue: 1` (EF writes `0`; change it to `1` so existing rows start at 1).
  EF rebuilds the SQLite table for the length change and check constraint; keep what it generates.
- [x] **B2.3** Edit `tests\GrantTracker.Data.Tests\Infrastructure\SqliteTestDatabase.cs`: change
  `InitialiseAsync()` to `InitialiseAsync(string? targetMigration = null)`; when
  `targetMigration` is not null call
  `await db.GetService<IMigrator>().MigrateAsync(targetMigration, cancellationToken)`
  (usings: `Microsoft.EntityFrameworkCore.Infrastructure`, `Microsoft.EntityFrameworkCore.Migrations`),
  otherwise `db.Database.MigrateAsync()` as today.
- [x] **B2.4** Create `tests\GrantTracker.Data.Tests\TaskSchemaTests.cs`:
  1. **Migration on existing data:** initialise to `"20261111112413_InitialCreate"`; with raw SQL
     (pattern in `ConstraintTests.cs`) insert one grant and one task with a 50-character title;
     then migrate to latest (`MigrateAsync()` on a new context); read the task: it still exists
     and `Version == 1`.
  2. **Length constraint:** on a fully migrated database a 100-character title inserts through EF;
     a 101-character title (raw SQL insert) throws `SqliteException` with
     `SqliteExtendedErrorCode == 275` (check constraint, as in `ConstraintTests`).
  3. **Concurrency token:** seed one grant and task; in two contexts of the same database load the task with
     `Tasks.Include(t => t.Grant)` and call `task.Grant!.CompleteTask(task, now)` in both; saving the first succeeds;
     saving the second throws `DbUpdateConcurrencyException` (a plain `context.SaveChangesAsync()`, not `EfUnitOfWork`).
  The existing `MigrationTests` must still pass (snapshot matches the model).

**Verify:** `cd D:\Project\backend; dotnet test`
**Done when:** all tests pass, including `MigrationTests`.
**Note:** existing databases with a title over 100 characters would block this migration; the seed has none.

### Step B3: Seed from the exercise (PDF section 6)

**Goal:** exactly 4 grants and 6 tasks, relative to today; the 5 officers stay; no assignment rows (Q10).

- [x] **B3.1** Rewrite `Data\Seeding\SeedData.cs`: keep `UserIds` and `Users`; **delete**
  `CreateGrantAssignments`, `CreateTaskAssignments` and the private `G`/`T` helpers; replace
  `CreateGrants` and `AddTasks` bodies with:

  ```csharp
  public static Grant[] CreateGrants(DateOnly today, DateTime now) =>
  [
      new(Id(101), "GR-100001", "Riverside Community Garden Inc.", today.AddDays(-60), GrantStatus.Open, null),
      new(Id(102), "GR-100002", "Northside Youth Sports Club", today.AddDays(-20), GrantStatus.Open, null),
      new(Id(103), "GR-100003", "Harbour Arts Collective", today.AddDays(-200), GrantStatus.Closed, now.AddDays(-140)),
      new(Id(104), "GR-100004", "Hillview Library Friends Group", today.AddDays(-5), GrantStatus.Open, null)
  ];

  public static void AddTasks(GrantTrackerDbContext db, DateOnly today, DateTime now) =>
      db.Tasks.AddRange(
          Task(201, "Verify bank details", 101, today.AddDays(-3), TaskPriority.High, GrantTaskStatus.Pending, null, now.AddDays(-60)),
          Task(202, "Request quote for tools", 101, today.AddDays(5), TaskPriority.Medium, GrantTaskStatus.Pending, null, now.AddDays(-60)),
          Task(203, "Check eligibility documents", 101, today.AddDays(-50), TaskPriority.High, GrantTaskStatus.Completed, now.AddDays(-52), now.AddDays(-60)),
          Task(204, "Review progress report", 102, today.AddDays(-1), TaskPriority.Low, GrantTaskStatus.Pending, null, now.AddDays(-20)),
          Task(205, "Schedule site visit", 102, today.AddDays(10), TaskPriority.Medium, GrantTaskStatus.Pending, null, now.AddDays(-20)),
          Task(206, "Final acquittal review", 103, today.AddDays(-150), TaskPriority.High, GrantTaskStatus.Completed, now.AddDays(-152), now.AddDays(-200)));
  ```

  Keep the `Id(int)` and `Task(...)` private helpers. Remove unused `using` lines
  (`GrantTracker.Core.Assignments`).
- [x] **B3.2** Edit `Data\Seeding\DbSeeder.cs`: delete the two blocks that add
  `TaskAssignments` and `GrantAssignments`; change the final log text to
  "Database seeded with demo officers, grants and tasks."
- [x] **B3.3** Rewrite `tests\GrantTracker.Data.Tests\DbSeederTests.cs` (clock
  `FakeBusinessClock(new DateTime(2026, 3, 10, 9, 0, 0), new DateOnly(2026, 3, 10))`): 5 users,
  4 grants, 6 tasks, 0 `GrantAssignments`, 0 `TaskAssignments`; "Verify bank details" due
  `2026-03-07`; "Review progress report" due `2026-03-09`; GR-100003 is `Closed` and GR-100004 has
  no tasks; GR-100001 has exactly one pending High task; a second `SeedAsync()` leaves 6 tasks.

**Verify:** `cd D:\Project\backend; dotnet test`
**Done when:** all tests pass. (An existing `grant-tracker.db` keeps the old data until deleted; see D3.)

### Step B4: Use cases, DTOs and ports in Core

**Goal:** every endpoint has a mediator request and handler, unit-tested with Moq. No Data or
Api code yet.

- [x] **B4.1** Ports. Create `Core\Grants\IGrantRepository.cs`:

  ```csharp
  namespace GrantTracker.Core.Grants;

  public sealed record GrantSummary(Guid Id, string ReferenceNumber, string OrganisationName,
      DateOnly ApplicationDate, GrantStatus Status, int TotalTasks, int CompletedTasks, int PendingHighTasks);

  public interface IGrantRepository
  {
      Task<Grant?> FindAsync(Guid id, CancellationToken cancellationToken = default);
      Task<Grant?> FindWithTasksAsync(Guid id, CancellationToken cancellationToken = default);
      Task<IReadOnlyList<GrantSummary>> ListAsync(GrantStatus? status, CancellationToken cancellationToken = default);
  }
  ```

  Create `Core\Tasks\ITaskRepository.cs`:

  ```csharp
  namespace GrantTracker.Core.Tasks;

  public interface ITaskRepository
  {
      void Add(GrantTask task);
      Task<GrantTask?> FindWithGrantAsync(Guid id, CancellationToken cancellationToken = default);
      Task<IReadOnlyList<GrantTask>> ListOverdueAsync(DateOnly today, CancellationToken cancellationToken = default);
  }
  ```

  `ListOverdueAsync` returns tasks with `Grant` loaded.
- [x] **B4.2** DTOs (records, namespace `GrantTracker.Core.Grants` or `.Tasks`; they are serialised by
  the idempotency store with default `System.Text.Json`, so keep them plain records):

  ```csharp
  public sealed record TaskDto(Guid Id, Guid GrantId, string Title, TaskPriority Priority, DateOnly DueDate,
      GrantTaskStatus Status, DateTime? CompletedAtUtc, bool IsOverdue, int DaysOverdue, int Version)
  {
      public static TaskDto From(GrantTask t, DateOnly today) => new(t.Id, t.GrantId, t.Title, t.Priority,
          t.DueDate, t.Status, t.CompletedAtUtc, t.IsOverdue(today), t.DaysOverdue(today), t.Version);
  }

  public sealed record GrantListItemDto(Guid Id, string ReferenceNumber, string OrganisationName,
      DateOnly ApplicationDate, GrantStatus Status, int TotalTasks, int CompletedTasks,
      int PendingHighTasks, bool CanClose)
  {
      public static GrantListItemDto From(GrantSummary s) => new(s.Id, s.ReferenceNumber, s.OrganisationName,
          s.ApplicationDate, s.Status, s.TotalTasks, s.CompletedTasks, s.PendingHighTasks,
          GrantRules.CanClose(s.Status, s.PendingHighTasks));

      public static GrantListItemDto From(Grant g) => new(g.Id, g.ReferenceNumber, g.OrganisationName,
          g.ApplicationDate, g.Status, g.Tasks.Count,
          g.Tasks.Count(t => t.Status == GrantTaskStatus.Completed), g.PendingHighTaskCount, g.CanClose);
  }

  public sealed record GrantDetailDto(Guid Id, string ReferenceNumber, string OrganisationName,
      DateOnly ApplicationDate, GrantStatus Status, DateOnly Today, int PendingHighTasks, bool CanClose,
      IReadOnlyList<TaskDto> Tasks);

  public sealed record OverdueTaskDto(Guid Id, Guid GrantId, string GrantReferenceNumber, string OrganisationName,
      GrantStatus GrantStatus, string Title, TaskPriority Priority, DateOnly DueDate, int DaysOverdue, int Version);
  ```

  Place `TaskDto` and `OverdueTaskDto` in `Core\Tasks\`, the grant DTOs in `Core\Grants\`.
- [x] **B4.3** Use-case folders (one request record and one handler each; handlers are `sealed`
  with primary constructors and implement `IRequestHandler<TRequest, TResponse>`; queries
  implement `IQuery<T>`, commands `ICommand<T>`):

  | Folder | Request | Response | Handler summary |
  | --- | --- | --- | --- |
  | `Core\Grants\ListGrants\` | `ListGrantsQuery(string? Status) : IQuery<IReadOnlyList<GrantListItemDto>>` | list | See code below |
  | `Core\Grants\GetGrant\` | `GetGrantQuery(Guid GrantId) : IQuery<GrantDetailDto>` | detail | `FindWithTasksAsync` or `NotFoundException("Grant not found.")`; `today = clock.Today`; tasks ordered by `DueDate` then `Title` mapped with `TaskDto.From(t, today)`; `PendingHighTasks = grant.PendingHighTaskCount`, `CanClose = grant.CanClose` |
  | `Core\Tasks\ListOverdueTasks\` | `ListOverdueTasksQuery() : IQuery<IReadOnlyList<OverdueTaskDto>>` | list | `tasks.ListOverdueAsync(clock.Today)` mapped (`DaysOverdue(today)`, grant fields from `task.Grant!`) |
  | `Core\Tasks\AddTask\` | `AddTaskCommand(Guid GrantId, string? Title, DateOnly? DueDate, TaskPriority? Priority, string? IdempotencyKey) : ICommand<TaskDto>, IIdempotentCommand` | task | See code below |
  | `Core\Tasks\CompleteTask\` | `CompleteTaskCommand(Guid TaskId) : ICommand<TaskDto>` | task | See code below |
  | `Core\Grants\CloseGrant\` | `CloseGrantCommand(Guid GrantId) : ICommand<GrantListItemDto>` | grant | See code below |

  `ListGrantsHandler(IGrantRepository grants)`:

  ```csharp
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
      return summaries.Select(GrantListItemDto.From).ToList();
  }
  ```

  `AddTaskHandler(IGrantRepository grants, ITaskRepository tasks, IBusinessClock clock, IAuditContext audit)`:

  ```csharp
  audit.SetTarget("Grant", request.GrantId);
  var grant = await grants.FindAsync(request.GrantId, cancellationToken)
      ?? throw new NotFoundException("Grant not found.");
  var task = grant.AddTask(Guid.NewGuid(), request.Title, request.DueDate, request.Priority, clock.Today, clock.UtcNow);
  tasks.Add(task);
  return TaskDto.From(task, clock.Today);
  ```

  `tasks.Add(task)` is required: EF treats a new entity with a client-set Guid key reachable
  only through a navigation as an existing row; the explicit `Add` marks it as new.

  `CompleteTaskHandler(ITaskRepository tasks, IBusinessClock clock, IAuditContext audit)`:

  ```csharp
  audit.SetTarget("Task", request.TaskId);
  var task = await tasks.FindWithGrantAsync(request.TaskId, cancellationToken)
      ?? throw new NotFoundException("Task not found.");
  if (!task.Grant!.CompleteTask(task, clock.UtcNow)) audit.SetOutcome(AuditOutcome.NoChange);
  return TaskDto.From(task, clock.Today);
  ```

  `CloseGrantHandler(IGrantRepository grants, IBusinessClock clock, IAuditContext audit)`:

  ```csharp
  audit.SetTarget("Grant", request.GrantId);
  var grant = await grants.FindWithTasksAsync(request.GrantId, cancellationToken)
      ?? throw new NotFoundException("Grant not found.");
  if (!grant.Close(clock.UtcNow)) audit.SetOutcome(AuditOutcome.NoChange);
  return GrantListItemDto.From(grant);
  ```

- [x] **B4.4** Register the six handlers in `Api\DependencyInjection\CoreServiceCollectionExtensions.cs`
  next to `LoginHandler`, e.g.
  `services.AddScoped<IRequestHandler<AddTaskCommand, TaskDto>, AddTaskHandler>();` (explicit, one line each).
- [x] **B4.5** Create `tests\GrantTracker.Core.Tests\Common\Time\FakeBusinessClock.cs`:
  `internal sealed class FakeBusinessClock(DateTime utcNow, DateOnly today) : IBusinessClock`
  (same shape as the Data test one; `UtcNow` is `DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)`).
- [x] **B4.6** Core handler tests (Moq for `IGrantRepository`/`ITaskRepository`, the real `AuditContext`,
  `FakeBusinessClock`; folders mirror the use cases):

  | Handler | Cases |
  | --- | --- |
  | `ListGrantsHandler` | `null`, `""` → repository called with `null`; `"open"`, `" CLOSED "` → `Open`/`Closed`; `"Foo"`, `"1"`, `"Open1"` → `ValidationException` with `Errors["status"]`; `CanClose` follows `GrantRules` (Open/0 true, Open/2 false, Closed/0 false) |
  | `GetGrantHandler` | unknown id → `NotFoundException`; maps `Today` from the clock, tasks ordered by due date, `isOverdue`/`daysOverdue` for a pending task due 3 days ago, `canClose` false when a pending High exists |
  | `ListOverdueTasksHandler` | maps grant reference, organisation, `grantStatus`, `daysOverdue` |
  | `AddTaskHandler` | unknown grant → `NotFoundException` and `Add` never called; success → `Add` called once, DTO fields, audit target `("Grant", id)`; closed grant → `BR-1`, `Add` never called; invalid input on a closed grant → `ValidationException` |
  | `CompleteTaskHandler` | unknown → `NotFoundException`; first complete → outcome `Changed`, `Version` 2, `completedAtUtc` set; repeat → outcome `NoChange`, same date; pending task on a closed grant → `GRANT_CLOSED`; completed task on a closed grant → `NoChange` (no exception) |
  | `CloseGrantHandler` | unknown → `NotFoundException`; pending High → `BR-5`; only Medium/Low pending → closes, DTO `status` `Closed`, `canClose` false; repeat → `NoChange` |

**Verify:** `cd D:\Project\backend; dotnet test`
**Done when:** all tests pass; `dotnet build` has no warnings from the new code.

### Step B5: Repositories in Data (+ integration tests)

**Goal:** the ports are implemented with EF and proven against SQLite.

- [x] **B5.1** Create `Data\Repositories\GrantRepository.cs`:

  ```csharp
  public sealed class GrantRepository(GrantTrackerDbContext db) : IGrantRepository
  {
      public Task<Grant?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
          db.Grants.SingleOrDefaultAsync(g => g.Id == id, cancellationToken);

      public Task<Grant?> FindWithTasksAsync(Guid id, CancellationToken cancellationToken = default) =>
          db.Grants.Include(g => g.Tasks).SingleOrDefaultAsync(g => g.Id == id, cancellationToken);

      public async Task<IReadOnlyList<GrantSummary>> ListAsync(GrantStatus? status, CancellationToken cancellationToken = default)
      {
          var query = db.Grants.AsNoTracking();
          if (status is { } wanted) query = query.Where(g => g.Status == wanted);
          return await query
              .OrderBy(g => g.Status == GrantStatus.Open ? 0 : 1)
              .ThenBy(g => g.ApplicationDate)
              .ThenBy(g => g.ReferenceNumber)
              .Select(g => new GrantSummary(g.Id, g.ReferenceNumber, g.OrganisationName, g.ApplicationDate, g.Status,
                  g.Tasks.Count,
                  g.Tasks.Count(t => t.Status == GrantTaskStatus.Completed),
                  g.Tasks.Count(t => t.Status == GrantTaskStatus.Pending && t.Priority == TaskPriority.High)))
              .ToListAsync(cancellationToken);
      }
  }
  ```

  Create `Data\Repositories\TaskRepository.cs`:

  ```csharp
  public sealed class TaskRepository(GrantTrackerDbContext db) : ITaskRepository
  {
      public void Add(GrantTask task) => db.Tasks.Add(task);

      public Task<GrantTask?> FindWithGrantAsync(Guid id, CancellationToken cancellationToken = default) =>
          db.Tasks.Include(t => t.Grant).SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

      public async Task<IReadOnlyList<GrantTask>> ListOverdueAsync(DateOnly today, CancellationToken cancellationToken = default) =>
          await db.Tasks.AsNoTracking().Include(t => t.Grant)
              .Where(t => t.Status == GrantTaskStatus.Pending && t.DueDate < today)
              .OrderBy(t => t.DueDate)
              .ThenBy(t => t.Priority == TaskPriority.High ? 0 : t.Priority == TaskPriority.Medium ? 1 : 2)
              .ThenBy(t => t.Title)
              .ToListAsync(cancellationToken);
  }
  ```

  (Verified while writing this plan: EF Core translates the `DateOnly` comparison, the
  `CASE` ordering and the count projection on SQLite.)
- [x] **B5.2** Register in `Data\DataServiceCollectionExtensions.cs`:
  `services.AddScoped<IGrantRepository, GrantRepository>();` and
  `services.AddScoped<ITaskRepository, TaskRepository>();` (add the usings).
- [x] **B5.3** Data tests, new file `tests\GrantTracker.Data.Tests\GrantQueryTests.cs` (seed with
  `DbSeeder` and the 2026-03-10 clock, then use new contexts or `ChangeTracker.Clear()`):

  | Test | Expectation |
  | --- | --- |
  | `ListAsync(null)` | 4 grants in this order: GR-100001 (applied 01-09), GR-100002 (02-18), GR-100004 (03-05), then GR-100003 (Closed). Open grants come first, oldest application date first |
  | `ListAsync(Open)` / `ListAsync(Closed)` | 3 / 1 grants |
  | Counts | GR-100001 → total 3, completed 1, pendingHigh 1; GR-100002 → 2, 0, 0; GR-100003 → 1, 1, 0; GR-100004 → 0, 0, 0 |
  | `FindWithTasksAsync` | returns 3 tasks for GR-100001; unknown id → `null` |
  | `FindAsync` + `AddTask` + `tasks.Add` + `SaveChanges` | the new task is **inserted** (not updated) and readable with `Version == 1` |
  | `ListOverdueAsync(2026-03-10)` | exactly "Verify bank details" (3 days) then "Review progress report" (1 day); a completed overdue task is excluded |
  | Overdue includes closed grants (Q15) | add a pending past-due task to GR-100003 with raw EF; it appears with `Grant.Status == Closed` |
  | `FindWithGrantAsync` | `Grant` populated; unknown id → `null` |

**Verify:** `cd D:\Project\backend; dotnet test`
**Done when:** all tests pass.

### Step B6: Controllers, CORS and API tests

**Goal:** every endpoint of section 3 works over HTTP with the right status, code and body.

- [x] **B6.1** Create `Api\Controllers\GrantsController.cs` (the repo's controllers live in feature
  folders such as `Auth\`; `Controllers\` already exists):

  ```csharp
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
  ```

  Create `Api\Controllers\AddTaskRequest.cs`:
  `public sealed record AddTaskRequest(string? Title, DateOnly? DueDate, TaskPriority? Priority);`
  (nullable members, no data annotations: validation is a Core concern).
  Create `Api\Controllers\TasksController.cs` (`[Route("tasks")]`):
  `[HttpPut("{id:guid}/complete")]` → `CompleteTaskCommand`, set the `ETag` header, return `Ok(task)`;
  `[HttpGet("overdue")]` → `ListOverdueTasksQuery`, return `Ok(list)`.
- [x] **B6.2** Edit `Api\Program.cs`: add `.WithExposedHeaders("ETag")` to the CORS policy.
- [x] **B6.3** Test infrastructure in `tests\GrantTracker.Api.Tests\Infrastructure\`:
  - `FakeBusinessClock.cs`: `public sealed class FakeBusinessClock : IBusinessClock` with settable
    `UtcNow` and `Today`, defaults `2026-03-10T09:00:00Z` and `2026-03-10`.
  - `SeedIds.cs`: `public static class SeedIds` with `Alex = Guid.Parse("00000000-0000-0000-0000-000000000001")`,
    `Grant1`..`Grant4` = `...000000000101`..`104`, `VerifyBankDetails` = `...201`, `RequestQuote` = `...202`,
    `EligibilityDocuments` = `...203` (completed), `ReviewProgressReport` = `...204`, `SiteVisit` = `...205`,
    `FinalAcquittal` = `...206` (task of the closed grant).
  - Edit `GrantTrackerApiFactory.cs`: add `public FakeBusinessClock Clock { get; } = new();` and inside
    `ConfigureTestServices` add `services.RemoveAll<IBusinessClock>(); services.AddSingleton<IBusinessClock>(Clock);`
    (using `Microsoft.Extensions.DependencyInjection.Extensions`). The seed at start-up then uses the fixed day.
- [x] **B6.4** Api tests (`tests\GrantTracker.Api.Tests\Grants\GrantsEndpointsTests.cs` and
  `Tasks\TasksEndpointsTests.cs`). Each test creates `new GrantTrackerApiFactory()` and
  `factory.CreateClient(SeedIds.Alex)` (`using` both), reads JSON with
  `ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)`. Cases:

  | Test | Request | Expected |
  | --- | --- | --- |
  | List all | `GET /grants` | 200, 4 items; first is GR-100001; last is GR-100003 (Closed); item 1 has `totalTasks` 3, `completedTasks` 1, `pendingHighTasks` 1, `canClose` false |
  | List filtered | `GET /grants?status=open` | 200, 3 items, all `"Open"` |
  | List invalid | `GET /grants?status=Foo` and `?status=1` | 400, `code` `VALIDATION_FAILED`, `errors.status` present |
  | Detail | `GET /grants/{Grant1}` | 200; `today` `"2026-03-10"`; `applicationDate` `"2026-01-09"`; `canClose` false; "Verify bank details" has `isOverdue` true, `daysOverdue` 3, `version` 1; the completed task has `isOverdue` false |
  | Detail business time | set `factory.Clock.Today` to `2026-03-11` **before** the first `CreateClient()` call (the host, and so the seed, starts then) | `today` equals `"2026-03-11"` (proves the server clock is used, Q11) |
  | Detail unknown | `GET /grants/{random guid}` and `/grants/not-a-guid` | 404, `code` `NOT_FOUND` |
  | No user | any call without `X-User-Id` | 401 `USER_REQUIRED` |
  | Add task OK | `POST /grants/{Grant4}/tasks` with key and `{ "title": "Call applicant", "dueDate": "2026-03-10", "priority": "Low" }` | 201, `ETag` `"1"`, body `status` `Pending`, `isOverdue` false; a later `GET` shows it |
  | Add task replay | same request and key twice | both 201, identical `id`; the grant has exactly one new task |
  | Add task key reuse | same key, different body | 422 `IDEMPOTENCY_KEY_MISMATCH` |
  | Add task no key | no `Idempotency-Key` | 400 `IDEMPOTENCY_KEY_REQUIRED` |
  | BR-3 | blank title; 101-char title | 400 `BR-3`, `errors.title` |
  | BR-2 | due `2026-03-09` (yesterday); also due `2026-03-04` on Grant4 (before its application date `2026-03-05`, and also in the past) | 400 `BR-2`, `errors.dueDate`; the boundary `2026-03-10` (today) is accepted. "Before the application date but not in the past" cannot occur with the seed, so Core tests cover it |
  | Mixed errors | blank title and past due date | 400 `VALIDATION_FAILED`, both `errors.title` and `errors.dueDate` |
  | BR-1 | `POST /grants/{Grant3}/tasks` with valid body | 409 `BR-1` |
  | BR-1 vs input order (Q7) | `POST /grants/{Grant3}/tasks` with a blank title | 400 `BR-3` (not 409) |
  | Malformed body | `"priority": "Urgent"`, or `"dueDate": "tomorrow"` | 400 `MALFORMED_REQUEST` |
  | Complete | `PUT /tasks/{VerifyBankDetails}/complete` | 200, `status` `Completed`, `completedAtUtc` set, `ETag` `"2"` |
  | BR-4 repeat | complete twice | second 200 with the identical `completedAtUtc` (advance `factory.Clock.UtcNow` by an hour in between to prove it) |
  | Complete unknown | random guid | 404 `NOT_FOUND` |
  | Complete on a closed grant | (a) `PUT /tasks/{FinalAcquittal}/complete` (already completed, grant closed); (b) close Grant2, then `PUT /tasks/{ReviewProgressReport}/complete` | (a) 200 no-op, same body; (b) 409 `GRANT_CLOSED` |
  | BR-5 | `PUT /grants/{Grant1}/close` | 409 `BR-5` |
  | Close OK | `PUT /grants/{Grant4}/close`; then `PUT /grants/{Grant2}/close` | 200 `status` `Closed`, `canClose` false; Grant2 closes although Low/Medium tasks are pending |
  | Close repeat | close Grant4 twice | both 200, same body |
  | Close after completing | complete `VerifyBankDetails`, then close Grant1 | 200 |
  | Overdue list | `GET /tasks/overdue` | 200, 2 items: "Verify bank details" (`daysOverdue` 3) then "Review progress report" (1); each has `grantReferenceNumber`, `organisationName`, `grantStatus` |
  | Audit | after Add and a rejected Close | rows in `Audits` with outcomes `Changed` and `Rejected` and the error code `BR-5` (read through a scope from `factory.Services`, `GrantTrackerDbContext`) |

  Keep each test independent (its own factory). Group several assertions per scenario where it
  keeps the suite readable; the table is the minimum coverage, not a one-test-per-row rule.
- [x] **B6.5** Edit `Api\GrantTracker.Api.http`: add one request per endpoint (with `X-User-Id` and,
  for add task, an `Idempotency-Key` such as `{{$guid}}`), using the seeded ids from `SeedIds`.

**Verify:** `cd D:\Project\backend; dotnet test`
**Done when:** all tests pass.

### Step B7: Back-end checkpoint (manual)

- [x] Stop any running API. Delete `backend\src\GrantTracker.Api\App_Data\grant-tracker.db` and its `-shm`/`-wal` files.
- [x] Start the API: `cd D:\Project\backend; dotnet run --project src/GrantTracker.Api --launch-profile http` (async/background).
- [x] With `curl.exe` and header `X-User-Id: 00000000-0000-0000-0000-000000000001`, check:
  `GET http://localhost:5080/grants` returns 4 grants; `GET /grants/00000000-0000-0000-0000-000000000101`
  returns `pendingHighTasks` 1 and `canClose` false; `PUT /grants/00000000-0000-0000-0000-000000000101/close` returns 409 with
  `"code":"BR-5"`.
- [x] Stop the API process (by its process id).

**Done when:** the three responses match; no exceptions in the console log.

---

## 6. Front-end steps (phase 1)

Files below are relative to `D:\Project\frontend\`. The front end has no router (Q8); `MainPage`
swaps between the grants list and one grant's tasks.

### Step F1: Test tooling (approved in Q13)

- [x] **F1.1** Install exact versions (record them in `package.json`; use `--save-exact`):
  `cd D:\Project\frontend; npm.cmd install --save-dev --save-exact vitest jsdom @testing-library/react @testing-library/dom`.
  These four packages are the only new dependencies (`@testing-library/dom` is a required peer of
  `@testing-library/react`). Do **not** add `jest-dom`, `user-event` or `msw`.
- [x] **F1.2** Add scripts to `package.json`: `"test": "vitest run"` and `"test:watch": "vitest"`.
- [x] **F1.3** Edit `vite.config.ts` to import `defineConfig` from `vitest/config` and add

  ```ts
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    env: { VITE_API_BASE_URL: 'http://api.test' },
    restoreMocks: true,
  },
  ```

- [x] **F1.4** Create `src/test/setup.ts`:

  ```ts
  import { cleanup } from '@testing-library/react'
  import { afterEach } from 'vitest'

  afterEach(() => cleanup())

  // jsdom may not implement the native <dialog> methods.
  if (!HTMLDialogElement.prototype.showModal) {
    HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
      this.setAttribute('open', '')
    }
    HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
      this.removeAttribute('open')
      this.dispatchEvent(new Event('close'))
    }
  }
  ```

- [x] **F1.5** Create `src/test/renderWithStore.tsx` and `src/test/mockApi.ts`:
  - `mockApi(routes)` replaces `fetch` with `vi.stubGlobal('fetch', ...)`. `routes` is an array of
    `{ method, path, status?, body?, handler? }`; the stub receives the `Request` that `fetchBaseQuery` builds,
    matches `method` and `new URL(req.url).pathname + search`, returns `new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })`
    (problem responses use `application/problem+json`), records every call in an array that tests can assert on
    (method, URL, headers, parsed body), and throws a clear error for an unmatched request.
  - `renderWithStore(ui)` imports the real `store` from `src/app/store.ts`, dispatches
    `baseApi.util.resetApiState()` and `signedIn({ id: '00000000-0000-0000-0000-000000000001', name: 'Alex Nguyen', email: 'alex.nguyen@example.org' })`
    (check the `UserDto` shape in `sessionSlice.ts`), then renders `<Provider store={store}>{ui}</Provider>` with RTL `render`.
- [x] **F1.6** Create `src/api/apiError.test.ts` as the smoke test: `toApiError` of
  `{ status: 400, data: { code: 'BR-2', title: 'x', errors: { dueDate: ['a'] } } }` returns
  `kind: 'problem'`, `code: 'BR-2'`, `fieldErrors.duedate` equal to `['a']`; and `{ status: 'FETCH_ERROR' }` returns `kind: 'unreachable'`.

**Verify:** `cd D:\Project\frontend; npm.cmd run test; npm.cmd run lint; npm.cmd run build`
**Done when:** all three commands succeed.

### Step F2: API layer, formatting helpers and error routing

- [x] **F2.1** Create `src/features/grants/types.ts`:

  ```ts
  export type GrantStatus = 'Open' | 'Closed'
  export type GrantStatusFilter = 'All' | GrantStatus
  export type TaskPriority = 'Low' | 'Medium' | 'High'
  export type TaskStatus = 'Pending' | 'Completed'

  export interface TaskDto {
    id: string; grantId: string; title: string; priority: TaskPriority; dueDate: string
    status: TaskStatus; completedAtUtc: string | null; isOverdue: boolean; daysOverdue: number; version: number
  }
  export interface GrantListItemDto {
    id: string; referenceNumber: string; organisationName: string; applicationDate: string
    status: GrantStatus; totalTasks: number; completedTasks: number; pendingHighTasks: number; canClose: boolean
  }
  export interface GrantDetailDto {
    id: string; referenceNumber: string; organisationName: string; applicationDate: string
    status: GrantStatus; today: string; pendingHighTasks: number; canClose: boolean; tasks: TaskDto[]
  }
  export interface NewTask { title: string; dueDate: string; priority: TaskPriority }
  ```

- [x] **F2.2** Edit `src/api/baseApi.ts`: `tagTypes: ['Grant']` and add `refetchOnFocus: true` next to
  `refetchOnReconnect: true`, with this comment above the options (OQ-5): "Data can be stale between refetches (a
  High task added by another officer, or the business day passing midnight). The server still enforces the rules,
  and the screen shows the refusal." (`setupListeners` is already called in `store.ts`, which `refetchOnFocus` needs.)
- [x] **F2.3** Create `src/features/grants/grantsApi.ts` (inject into `baseApi`, pattern in `docs/patterns/frontend.md`):

  | Endpoint | Definition | Tags |
  | --- | --- | --- |
  | `getGrants` | `build.query<GrantListItemDto[], GrantStatusFilter>`; url `'/grants'` for `All`, else `` `/grants?status=${status}` `` | provides `{type:'Grant', id:'LIST'}` and `{type:'Grant', id}` for each item |
  | `getGrant` | `build.query<GrantDetailDto, string>`; url `` `/grants/${id}` `` | provides `{type:'Grant', id}` |
  | `addTask` | `build.mutation<TaskDto, { grantId: string; task: NewTask; idempotencyKey: string }>`; `POST /grants/${grantId}/tasks`, body `task`, header `'Idempotency-Key'` | invalidates `{type:'Grant', id: grantId}` on success and on `409` |
  | `completeTask` | `build.mutation<TaskDto, { taskId: string; grantId: string }>`; `PUT /tasks/${taskId}/complete` | invalidates `{type:'Grant', id: grantId}` on success and on `409` |
  | `closeGrant` | `build.mutation<GrantListItemDto, string>`; `PUT /grants/${id}/close` | invalidates `{type:'Grant', id}` and `{type:'Grant', id:'LIST'}` on success and on `409` |

  Use `invalidatesTags: (_result, error, arg) => error && error.status !== 409 ? [] : [...]`. Export the hooks
  (`useGetGrantsQuery`, `useGetGrantQuery`, `useAddTaskMutation`, `useCompleteTaskMutation`, `useCloseGrantMutation`).
  The `login` endpoint is the only one that omits `X-User-Id`; do not change that.
- [x] **F2.4** Create `src/features/grants/format.ts` (no JavaScript `Date`, to avoid time-zone shifts):

  ```ts
  const months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec']
  export function formatDate(iso: string): string {
    const [year, month, day] = iso.split('-')
    return `${day}-${months[Number(month) - 1]}-${year}`
  }
  export function overdueLabel(days: number): string {
    return `Overdue by ${days} ${days === 1 ? 'day' : 'days'}`
  }
  export function minimumDueDate(today: string, applicationDate: string): string {
    return today > applicationDate ? today : applicationDate
  }
  ```

  Also export `filterAndSortTasks(tasks, { status: 'All'|TaskStatus, priority: 'All'|TaskPriority, search: string })`:
  filter by status, priority and a case-insensitive `title.includes(search.trim())`; sort Pending before Completed,
  then priority High → Medium → Low, then `dueDate` ascending (ISO strings compare correctly), then title.
  Create `format.test.ts`: `formatDate('2026-03-07') === '07-Mar-2026'`; `overdueLabel(1)` "Overdue by 1 day",
  `overdueLabel(3)` "Overdue by 3 days"; `minimumDueDate` returns the later date either way; `filterAndSortTasks`
  covers status, priority, search and the three-level order.
- [x] **F2.5** Edit `src/api/errorMessages.ts` (OQ-6): add to `endpointActionPhrases`:
  `getGrants: 'Loading grants'`, `getGrant: 'Loading the grant'`, `addTask: 'Adding the task'`,
  `completeTask: 'Completing the task'`, `closeGrant: 'Closing the grant'`. Leave `codeMessages` as it is in phase 1
  (the business refusals are shown inline, not toasted); phase 2 adds `VERSION_CONFLICT` and `PRECONDITION_REQUIRED`
  only if they can reach a toast. Also export `getInlineErrorMessage(error: ApiError): string` returning, for a `problem`,
  `error.title`, except `IDEMPOTENCY_KEY_MISMATCH` → "This request doesn't match your earlier attempt. Check the details, then select Add task again.";
  for anything else "That didn't work. Try again."
- [x] **F2.6** Edit `src/api/errorListener.ts`: replace the ignore condition with a set so business refusals that the
  screen shows inline are not also toasted:

  ```ts
  const inlineCodes = new Set(['VALIDATION_FAILED', 'NOT_FOUND', 'BR-2', 'BR-3', 'IDEMPOTENCY_KEY_MISMATCH'])
  const inlineStatuses = new Set([409, 412, 428])
  // in the effect:
  if (error.kind === 'problem' && (inlineCodes.has(error.code) || inlineStatuses.has(error.status))) return
  ```

  `IDEMPOTENCY_KEY_REQUIRED` and `MALFORMED_REQUEST` keep their toast (the screen cannot fix them).
- [x] **F2.7** Edit `src/features/connection/OfflineBanner.tsx`: text becomes
  "Can't reach the server. Check your connection, then try again." (keep `<strong>Can&apos;t reach the server.</strong>`).
- [x] **F2.8** Tests: `src/api/errorListener.test.ts` is optional; required is `src/features/grants/grantsApi.test.tsx`:
  with `mockApi`, dispatching `grantsApi.endpoints.getGrants.initiate('Open')` requests `GET /grants?status=Open` and
  includes the `X-User-Id` header; `initiate('All')` requests `/grants`; `addTask` sends `Idempotency-Key` and a JSON body;
  after a successful `closeGrant` the cached `getGrants('Open')` is refetched (assert a second `GET /grants?status=Open`).

**Verify:** `cd D:\Project\frontend; npm.cmd run test; npm.cmd run lint; npm.cmd run build`
**Done when:** all pass; nothing in the UI changed yet (the app still shows the empty main page).

### Step F3: Theme additions (tokens only, no raw colours)

- [x] **F3.1** Append to `src/styles/theme.css`, using existing tokens (`--sheet`, `--rule`, `--ink`, `--ink-muted`,
  `--paper`, `--council`, `--council-tint`, `--stamp`, `--space-*`, `--radius-control`, `--shadow-float`, `--text-*`):
  - `.page` (max width about 72rem, centred, padding `--space-5`) and `.page-header` (flex, wrap, gap, title block and actions).
  - `.table-wrap` (`overflow-x: auto`, border `1px solid var(--rule)`, background `--sheet`) and `.table`
    (`width: 100%`, `border-collapse: collapse`; `th` left-aligned bold; `td`/`th` padding `--space-3`; row border-bottom `--rule`;
    `.table-actions` cell with flex gap; `tr.row-completed` background `--council-tint`).
  - `.select` (same look as `.input`; add `appearance: auto`), `.filter-bar` (flex, wrap, gap `--space-4`, each child a `.field`).
  - `.dialog` (for `<dialog>`: `max-width: 32rem`, width `calc(100% - 2rem)`, padding `--space-5`, border `1px solid var(--rule)`,
    radius, `box-shadow: var(--shadow-float)`; `.dialog::backdrop` uses `rgb(29 43 38 / 0.45)`), `.dialog-actions` (flex end, gap), `.dialog-title`.
  - `.empty` (muted text, padded) for empty states, and `.meta` (muted small text).
  Keep the CSS rules minimal and add each class only if a component uses it (F4 to F6 decide the final list).
- [x] **F3.2** Add a section to `.github\workflows\setup-repo\style-guide.html` showing the table, filter bar, select and dialog
  markup (UI-STYLE section 11 process). Do not edit `docs\UI-STYLE.md` here; step D2 does.

**Verify:** `cd D:\Project\frontend; npm.cmd run build; npm.cmd run lint` and open the style guide in a browser.
**Done when:** both succeed.

### Step F4: Grants list, navigation swap and Close grant

**Goal:** the main page shows the grants table with the status filter, Tasks and Close actions.

- [x] **F4.1** Create `src/features/grants/CloseGrantButton.tsx`.
  Props: `{ grant: Pick<GrantListItemDto, 'id' | 'referenceNumber' | 'status' | 'canClose' | 'pendingHighTasks'> }`.
  Uses `useCloseGrantMutation`. Behaviour:
  - Disabled when `!grant.canClose` or while the request runs. Label "Close grant"; pending label "Closing…".
  - When disabled by the rule it renders a reason in a `<p className="meta" id={`close-reason-${grant.id}`}>` and sets
    `aria-describedby` on the button: status `Closed` → "Already closed."; otherwise
    `${n} High priority ${n === 1 ? 'task' : 'tasks'} pending.`
  - Button class `btn btn-secondary`, `aria-label` `Close grant ${grant.referenceNumber}` (the visible text stays "Close grant").
  - A rejected call: `const error = toApiError(e)`; for `kind === 'problem'` show
    `<div className="alert" role="alert"><p className="alert-title">{getInlineErrorMessage(error)}</p></div>` next to the
    button, with the reference (existing `Reference` component, pass `traceId`) when present. Clear the alert on the next attempt. No success toast.
- [x] **F4.2** Create `src/features/grants/GrantsPage.tsx`.
  Props: `{ statusFilter: GrantStatusFilter; onStatusFilterChange(s): void; onOpenTasks(id: string): void; focusGrantId?: string }`.
  - `useGetGrantsQuery(statusFilter)`; a labelled `<select className="select">` "Status" with options All, Open, Closed inside a `.filter-bar`.
  - States: loading → `<p role="status">Loading grants…</p>`; error that is not "unreachable" →
    `<p role="alert">Grants couldn't be loaded.</p>`; empty → `<p className="empty">No grants match this filter.</p>`.
  - Table (`.table-wrap > table.table`, visually hidden `<caption>Grants</caption>`), columns: Reference, Organisation, Application date
    (`formatDate`), Status (`<span className="tag tag-open|tag-closed">` with the words Open/Closed), Tasks
    (`${completedTasks} / ${totalTasks}` completed), Actions (`.table-actions`): a `Tasks` button (`btn btn-secondary`,
    `aria-label` `Tasks for ${referenceNumber}`, `data-grant-id`) calling `onOpenTasks`, and `<CloseGrantButton />`.
  - If `focusGrantId` is set, after the data renders focus the Tasks button with that `data-grant-id` (focus returns on Back).
  - The page `<h1>` stays in `MainPage` (visually hidden "Grant Task Tracker" on the list).
- [x] **F4.3** Rewrite `src/features/main/MainPage.tsx` body (keep the header exactly as is). State:

  ```ts
  const [view, setView] = useState<{ kind: 'list' } | { kind: 'tasks'; grantId: string }>({ kind: 'list' })
  const [statusFilter, setStatusFilter] = useState<GrantStatusFilter>('Open')
  const [returnFocusGrantId, setReturnFocusGrantId] = useState<string>()
  ```

  List view renders the hidden `<h1>Grant Task Tracker</h1>` and `<GrantsPage … />`; tasks view renders `<GrantTasksPage grantId onBack>`
  (built in F5; until then render a placeholder `<p>Tasks</p>` with a Back button so this step compiles). `onBack` sets
  `view` to the list and `returnFocusGrantId` to that grant id. Signing out unmounts `MainPage`, so state cannot leak between officers.
  Add the small CSS needed to `MainPage.css`.
- [x] **F4.4** Component tests `src/features/grants/GrantsPage.test.tsx` (using `mockApi` and `renderWithStore`):
  | Test | Expectation |
  | --- | --- |
  | Renders rows | two grants appear with reference, organisation, `07-Mar-2026`-style date, status words and "1 / 3" counts |
  | Default filter | the first request is `GET /grants?status=Open`; choosing "All" requests `GET /grants` |
  | Close disabled | a grant with `canClose: false, pendingHighTasks: 1` has a disabled "Close grant" button described by "1 High priority task pending."; a Closed grant says "Already closed." |
  | Close success | clicking Close on an enabled grant sends `PUT /grants/{id}/close`; the list is refetched |
  | Close refused | a `409` with `{ code: 'BR-5', title: "This grant can't be closed while it has High priority tasks pending." }` shows that text in an `alert` beside the button, and no error toast is created (the listener ignores 409) |
  | Empty | `[]` shows "No grants match this filter." |
  | Open tasks | clicking "Tasks for GR-100001" calls `onOpenTasks('…')` |

**Verify:** `cd D:\Project\frontend; npm.cmd run test; npm.cmd run lint; npm.cmd run build`
**Done when:** all pass; with the API running (step B7) the list shows the 3 open grants.

### Step F5: Grant tasks view (read-only parts, Complete and Close)

- [x] **F5.1** Create `src/features/grants/GrantTasksPage.tsx`. Props `{ grantId: string; onBack(): void }`.
  - `const { data: grant, isFetching, refetch, error } = useGetGrantQuery(grantId, { refetchOnMountOrArgChange: true })`
    (OQ-2: always fresh when the view opens).
  - Heading: `<h1 tabIndex={-1} ref={headingRef}>{grant.referenceNumber} · {grant.organisationName}</h1>`; focus it when the data first
    arrives (keyboard and screen-reader users are told the view changed). Under it: "Applied {formatDate(applicationDate)}" and a status tag.
  - Header actions: `Back to grants` (`btn btn-quiet`, calls `onBack`), `<CloseGrantButton grant={grant} />`, and **Add task**
    (`btn btn-primary`, the only primary button in the view; disabled when `grant.status === 'Closed'`, with the reason
    "This grant is closed." linked by `aria-describedby`). Clicking it calls `refetch()` then opens the dialog (F6).
  - Filters (`.filter-bar`): Status select (All, Pending, Completed; **default Pending**), Priority select (All, High, Medium, Low),
    Search input labelled "Search by title". Use `filterAndSortTasks`.
  - Table columns: Title, Priority, Due date, Status, Actions.
    - Priority tag: High → `tag tag-high` "High priority"; Medium → `tag tag-medium` "Medium priority"; Low → `tag` "Low priority".
    - Due date: `formatDate(dueDate)`; when `isOverdue`, add `<span className="tag tag-overdue">{overdueLabel(daysOverdue)}</span>`
      (never red, Q3; completed tasks never show it because the API returns `isOverdue: false`).
    - Status tag: Completed → `tag tag-completed` "Completed"; Pending → `tag tag-open` "Pending"; completed rows get class `row-completed`.
    - Actions: **Complete** (`btn btn-secondary`, pending tasks only, `aria-label` `Complete ${title}`), disabled with the reason
      "This grant is closed." when the grant is closed, and with the label "Completing…" while its own request runs.
      A refused complete (`409 GRANT_CLOSED`) shows an inline `alert` in that row's actions cell. No success toast (the row visibly changes).
  - Empty states: grant has no tasks → "No task"; tasks exist but none match → "No task matches the filters".
  - Loading → "Loading tasks…" (`role="status"`); a `NOT_FOUND` error → alert "This grant no longer exists." with the Back button.
- [x] **F5.2** Edit `MainPage.tsx`: replace the placeholder with `<GrantTasksPage grantId onBack />`.
- [x] **F5.3** Tests `src/features/grants/GrantTasksPage.test.tsx` (mock `GET /grants/{id}` with a detail that has `today: '2026-03-10'`):
  | Test | Expectation |
  | --- | --- |
  | Rows and badges | priority words shown for each task; a pending overdue task shows "Overdue by 3 days"; a completed past-due task shows no overdue badge |
  | Default filter | only Pending tasks listed; choosing status "All" lists all; "Completed" lists only completed |
  | Priority and search | priority "High" hides others; search "bank" (any case) shows only matching titles |
  | Order | pending before completed, then High, Medium, Low, then due date |
  | Empty states | no tasks → "No task"; filters matching nothing → "No task matches the filters" |
  | Complete | clicking "Complete Verify bank details" sends `PUT /tasks/{id}/complete`, then the grant is refetched and the row shows Completed |
  | Complete pending label | the button shows "Completing…" and is disabled while the request is unresolved |
  | Closed grant read-only | `status: 'Closed'` → Add task and every Complete button are disabled and "This grant is closed." is present |
  | Close on the tasks view | uses the detail's `canClose`/`pendingHighTasks` (disabled with "1 High priority task pending.") |
  | Heading focus | the `h1` has focus after load |
  | Refetch on open | the first call is `GET /grants/{id}` and clicking Add task triggers another `GET /grants/{id}` |

**Verify:** `cd D:\Project\frontend; npm.cmd run test; npm.cmd run lint; npm.cmd run build`
**Done when:** all pass; with the API running, Tasks opens a grant and Complete works.

### Step F6: Add task dialog

- [x] **F6.1** Create `src/features/grants/AddTaskDialog.tsx`. Props
  `{ open: boolean; grant: { id: string; applicationDate: string; today: string }; onClose(): void }`.
  - A native `<dialog className="dialog" ref>`; a `useEffect` calls `showModal()` when `open` becomes true (and `close()` when false);
    listen to the dialog's `close` event to call `onClose`. Escape and the Cancel button close it.
  - Fields (each a `.field`): **Title** (`input.input`), **Priority** (`select.select`; options High, Medium, Low; default Medium),
    **Due date** (`input.input type="date"`, `min={minimumDueDate(today, applicationDate)}`). Hint under Title: "Up to 100 characters."
  - Client validation on submit (hints only; the server decides): title blank → "Enter a task title."; title over 100 →
    "The title must be 100 characters or fewer."; due date blank → "Enter a due date."; due date `< min` → "The due date can't be before {formatDate(min)}.".
    On failure focus the first invalid field. Errors: `<p className="field-error" id="…" role="alert">`, `aria-invalid`, `aria-describedby`.
  - Idempotency key: `const keyRef = useRef(crypto.randomUUID())`; replace it whenever any field changes (`onChange`) and after a successful
    submit; a manual retry with unchanged values reuses it.
  - Submit button `btn btn-primary` "Add task" (pending "Adding task…", disabled while running); Cancel is `btn btn-secondary`.
  - Server errors (`toApiError`): `400` → map `fieldErrors.title`, `fieldErrors.duedate`, `fieldErrors.priority` to the fields (first message each);
    `409 BR-1` → `<div className="alert" role="alert">` at the top of the dialog with `getInlineErrorMessage(error)`;
    `422 IDEMPOTENCY_KEY_MISMATCH` → same alert, and replace the key; other errors are handled by the global listener.
  - On success: reset the form, call `onClose()`. No toast (the table updates; OQ-6/UI-STYLE).
  - Reset the form and errors each time the dialog opens.
- [x] **F6.2** Wire into `GrantTasksPage.tsx`: `const [adding, setAdding] = useState(false)`; render
  `<AddTaskDialog open={adding} grant={{ id, applicationDate, today }} onClose={() => setAdding(false)} />` only when `grant` is loaded.
- [x] **F6.3** Tests `src/features/grants/AddTaskDialog.test.tsx`:
  | Test | Expectation |
  | --- | --- |
  | Date minimum | the date input's `min` is the later of `today` and the application date (`'2026-03-10'` and `'2026-03-15'` → `'2026-03-15'`) |
  | Client validation | submitting empty shows "Enter a task title." and "Enter a due date." and sends no request; a 101-character title shows the length message |
  | Success | valid input sends `POST /grants/{id}/tasks` with body `{ title, dueDate, priority }` and a non-empty `Idempotency-Key`, then calls `onClose` |
  | Key reuse | after a failed submit with unchanged values, the retry sends the **same** key; after editing a field it sends a **different** key |
  | Server field errors | a `400` with `{ code: 'BR-2', errors: { dueDate: ["The due date can't be in the past."] } }` shows that text beside the due date and no toast |
  | BR-1 | a `409` `BR-1` shows the title text in an `alert` inside the dialog |
  | 422 | a `422 IDEMPOTENCY_KEY_MISMATCH` shows the mismatch message in the dialog and the next submit uses a new key |
  | Pending label | the primary button reads "Adding task…" and is disabled while the request is unresolved |
  | Reopen | closing and reopening clears errors and values |

**Verify:** `cd D:\Project\frontend; npm.cmd run test; npm.cmd run lint; npm.cmd run build`
**Done when:** all pass.

### Step F7: Front-end checkpoint (manual, with the API running)

- [x] Start the API (step B7, fresh database) and `npm.cmd run dev`; sign in as `alex.nguyen@example.org`.
- [x] Walk the exercise scenario: list shows GR-100001, GR-100002, GR-100004 (Open) in application-date order; GR-100001's Close is disabled
  with "1 High priority task pending."; open its tasks, see "Verify bank details" overdue by 3 days; Complete it; return to the list and
  Close GR-100001 (now allowed); filter Closed shows GR-100003; its tasks view has Add task and Complete disabled with "This grant is closed."
- [x] Add a task with a past date and see the server message beside the date; add a valid task and see it in the table.
- [x] Check at 320 px wide (browser dev tools): the table scrolls horizontally inside `.table-wrap`, nothing overflows the page.
- [ ] Keyboard only: Tab order is logical, the dialog traps focus and closes on Escape, focus lands on the grant heading after Tasks and on that grant's
  Tasks button after Back.
- [x] Stop the API and press Complete: the banner reads "Can't reach the server. Check your connection, then try again."

**Done when:** every item behaves as described. (Native `<dialog>` and focus cannot be fully proven by jsdom, which is why this is manual.)

---

## 7. Documentation steps (approved in Q13; same change as the behaviour)

### Step D1: `docs/patterns/backend.md` and `docs/patterns/frontend.md`

- [x] Add worked examples, **copied from the compiled and passing code** (each example cites its source file path; never written from memory):
  1. A rule on the entity with `today` passed in (`Grant.AddTask` and `TaskRules.Validate`).
  2. A use-case folder: query and handler (`ListGrants`), command and handler with audit target and `NoChange` (`CompleteTask`).
  3. A repository port and its EF implementation (`IGrantRepository` and `GrantRepository`), including why `tasks.Add(task)` is explicit.
  4. A controller action and request DTO (`GrantsController.AddTask`, `AddTaskRequest`).
  5. DI registration of a handler and a repository.
  6. A new error code and its HTTP mapping (and, after phase 2, a new exception type with its mapping).
  7. An idempotent command (`AddTaskCommand`, key from the header, replay).
  8. One test at each level: Core with Moq and `FakeBusinessClock`; Data with `SqliteTestDatabase`; Api with `GrantTrackerApiFactory` and its fake clock.
- [x] `frontend.md`: examples copied from `grantsApi.ts` (queries with tags, mutation invalidation on 409), `AddTaskDialog.tsx`
  (idempotency key per intent, mapping `fieldErrors`), the `mockApi`/`renderWithStore` test pattern, and the staleness comment.

**Done when:** every example matches its source file byte for byte (re-check in step V).

### Step D2: `ARCHITECTURE.md`, `ENTITIES.json`, `UI-STYLE.md`

- [x] `docs\ARCHITECTURE.md`: error table (rows for `BR-1`, `BR-2`, `BR-3`, `BR-5`, `GRANT_CLOSED`, and after phase 2 `412`, `428`, `TASK_COMPLETED`);
  CORS row (exposes `ETag`; after phase 2 allows `If-Match`); data model table notes that the assignment tables are present but unused by the grant
  and task features; testing table notes the Api fake clock; known limitations add "lists are unpaged" and "data can be stale between refetches".
- [x] `docs\ENTITIES.json`: `Tasks.Title string(100)` with note `CK_Tasks_Title_Length`; add `Version int [not null, default: 1, note: "optimistic concurrency token"]`;
  mark the assignment tables "present, not used by the grant and task features".
- [x] `docs\UI-STYLE.md`: section 6 add table, select, filter bar, dialog; section 7 offline banner text becomes "Can't reach the server. Check your connection,
  then try again." and the "Client mistakes" row becomes "`MALFORMED_REQUEST`, `IDEMPOTENCY_KEY_REQUIRED`, 5xx, anything unexpected" with
  "`IDEMPOTENCY_KEY_MISMATCH` (422) in a dialog → inline alert in the dialog"; section 10 replaces "Main page: blank for now" with the grants list and tasks view.

### Step D3: `README.md`

- [x] Add: the business time zone (`Asia/Singapore`, and why "today" comes from the server); how to run the front-end tests
  (`cd frontend; npm.cmd run test`); **Reset the demo** now also says an existing database keeps the old seed until deleted; assumptions:
  BR-4 repeat complete returns `200` unchanged (and why: a retry after a lost response is harmless), closing a closed grant returns `200`,
  only a pending High task blocks Close (BR-5), a closed grant is read-only, overdue tasks of closed grants stay on `GET /tasks/overdue` (Q15), and
  the status values are case-insensitive; **Known limitations**: lists are unpaged (OQ-8), the grants list, `canClose` and `today` can be stale between
  refetches (refetch on open and on focus, no polling; the server still decides) (OQ-5), pending Medium/Low tasks on a closed grant cannot be completed,
  no authorisation, and (if phase 2 is cut) the `Version` column and codes ship but nothing enforces them.

**Done when:** the three docs match the final code and behaviour.

---

## 8. Phase 2: Edit task (extra, cuttable; do only after phase 1 is green)

### Step E1: Core and Data

- [x] `ErrorCodes`: `TaskCompleted = "TASK_COMPLETED"`, `VersionConflict = "VERSION_CONFLICT"`, `PreconditionRequired = "PRECONDITION_REQUIRED"`.
- [x] Create `Core\Common\Errors\VersionConflictException.cs`:
  `public sealed class VersionConflictException() : AppException(ErrorCodes.VersionConflict, "This task was changed by someone else before your edit was saved. Reload to see the latest version, then edit again.");`
  and `PreconditionRequiredException.cs` (`ErrorCodes.PreconditionRequired`, "Send the task's current version in the If-Match header."). (OQ-7)
- [x] `Grant.EditTask(GrantTask task, string? title, DateOnly? dueDate, TaskPriority? priority, DateOnly today, DateTime nowUtc)` in Core: task must belong to the grant;
  closed grant → `GRANT_CLOSED`; completed task → `ConflictException(TASK_COMPLETED, "A completed task can't be edited.")`;
  `TaskRules.Validate(..., enforceDueDate: dueDate != task.DueDate)` (BR-3 always, BR-2 only when the date changes); then `task.Edit(valid)` which sets
  the three fields and increments `Version`.
- [x] `EditTaskCommand(Guid TaskId, int? ExpectedVersion, string? Title, DateOnly? DueDate, TaskPriority? Priority, string? IdempotencyKey) : ICommand<TaskDto>, IIdempotentCommand` and
  handler in `Core\Tasks\EditTask\`: audit target `("Task", id)`; 404; `ExpectedVersion is null` → `PreconditionRequiredException`; `task.Version != ExpectedVersion` → `VersionConflictException`;
  then `Grant.EditTask`. The idempotency behaviour runs before the handler, so a lost-response retry replays the first result instead of a false 412 (Q14). Register the handler.
- [x] `EfUnitOfWork.SaveChangesAsync`: catch `DbUpdateConcurrencyException` and throw `VersionConflictException` (so the audit behaviour records `Rejected`); this also covers a race in Complete.
- [x] Core tests: stale version → `VersionConflictException`; missing version → `PreconditionRequiredException`; unchanged late due date saves; changed due date in the past → `BR-2`;
  completed task → `TASK_COMPLETED`; closed grant → `GRANT_CLOSED`; `Version` increments. Data test (extend `TaskSchemaTests`): the race between two contexts now surfaces as `VersionConflictException` through `EfUnitOfWork`.

### Step E2: Api

- [x] `AppExceptionHandler` switch: `VersionConflictException => (412, …)`, `PreconditionRequiredException => (428, …)`.
- [x] `TasksController`: `[HttpPut("{id:guid}")]` reads `[FromHeader(Name = "If-Match")] string? ifMatch` and `Idempotency-Key`; parse `"3"` (quotes optional) to `int?`; an unparsable value throws
  `ValidationException(ErrorCodes.MalformedRequest, "If-Match must contain the task's version.", …)`; returns `Ok(task)` with `ETag`. `EditTaskRequest(string? Title, DateOnly? DueDate, TaskPriority? Priority)`.
- [x] `Program.cs` CORS: add `"If-Match"` to `WithHeaders`.
- [x] Api tests: missing `If-Match` → 428; stale → 412 with the OQ-7 title; correct → 200, `ETag` `"2"`; replay of the same key after a success → 200 (not 412); completed → 409 `TASK_COMPLETED`; closed grant → 409 `GRANT_CLOSED`; invalid body → 400; an `ETag` is present on add and complete responses.

### Step E3: Front end

- [x] `grantsApi.ts`: `editTask` mutation (`PUT /tasks/${taskId}`, headers `If-Match: "${version}"` and `Idempotency-Key`; invalidates the grant tag on success, on `409`, `412`).
- [x] Rename `AddTaskDialog` to `TaskFormDialog` with `mode: 'add' | 'edit'` (edit prefills title, priority, due date, uses the task's `version`, title "Edit task", submit "Save changes", pending "Saving…");
  in edit mode the date `min` is **not** enforced client-side unless the date changed.
- [x] `GrantTasksPage`: **Edit** button (`btn btn-secondary`, pending tasks only, disabled on a closed grant with "This grant is closed.").
- [x] `errorListener.ts` already ignores 412/428 (F2.6); the dialog shows `getInlineErrorMessage` for `412` (the API `title`, the OQ-7 text) and invalidates tags so the list refreshes.
- [x] Tests: Edit dialog prefill; successful save sends `If-Match` and `Idempotency-Key`; `412` shows the conflict text inside the dialog; closed grant disables Edit.

### Step E4: Phase 2 docs

- [x] Update `backend.md` (a worked 412/428 example), `frontend.md`, `ARCHITECTURE.md` (412/428 rows, CORS), `README.md` (Edit rule, concurrency, retries).

**Verify (each step):** `dotnet test` and `npm.cmd run test; npm.cmd run build; npm.cmd run lint`.
**Done when:** all pass and a manual edit conflict (two browser tabs) shows the OQ-7 message.

---

## 9. Final validation (step V)

| Who | What |
| --- | --- |
| Agent | `cd D:\Project\backend; dotnet build; dotnet test`; `cd D:\Project\frontend; npm.cmd run test; npm.cmd run lint; npm.cmd run build`; start from a deleted database and run the checks of steps B7 and F7; re-check every doc example against its source file |
| Developer | Browser check of dialog focus, the 320 px layout and another time zone (set the OS or browser zone; the screen must still show the server's "today"); the seeded BR-5 and BR-6 demo; the live-change readiness |

- [x] Backend build and all tests pass, with no new warnings from the new code.
- [x] Frontend tests, lint and build pass.
- [ ] Manual checkpoints B7 and F7 done on a freshly reset database.
- [x] Docs D1 to D3 (and E4 if phase 2 was built) match the code.
- [x] No leftover temporary files (`ScratchTests.cs`, `out.txt`, `bin`/`obj` are ignored by git).

### Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| SQLite rebuilds the `Tasks` table in the migration | Test B2.4(1) applies it on top of `InitialCreate` with data present |
| Existing databases keep the old seed | README reset steps (D3) |
| A new `GrantTask` reachable only through a navigation is treated as existing (client-set Guid key) | `tasks.Add(task)` is explicit; Data test in B5.3 inserts through that path |
| `canClose` stale after another officer adds a High task | The server refuses with `BR-5`; the screen shows the reason (OQ-5) |
| A race on Complete in phase 1 raises `DbUpdateConcurrencyException` (500) until E1 translates it | Rare, documented; E1 maps it to `412` |
| `jsdom` lacks the native `<dialog>` methods | Polyfill in `src/test/setup.ts` (F1.4); real behaviour is checked manually (F7) |
| `crypto.randomUUID` is unavailable on non-secure origins | The app runs on `localhost`; documented in the README |
| Docs drift from code | Examples copied from compiled code, re-checked in step V |
| Edit adds a lot of surface late | Phase 2 is separable; if cut, the `Version` column and codes ship unenforced and the README says so |

### Known limitations of this plan

- Response shapes (section 3) are proposed here; changing them later affects front-end types and tests.
- `canClose` and `pendingHighTasks` are both returned although `canClose` is derivable; this is deliberate so the rule lives in one place (OQ-1).
- No polling: data can be out of date between refetches (OQ-5). The grants and task lists are unpaged (OQ-8).
- Native `<dialog>` and focus behaviour cannot be fully verified by component tests.
- Table rows in section 5 tests are minimum coverage; add cases if a rule has an edge that is not listed.

---

## 10. Open Questions

> Each question lists what it is, why it matters, how much it affects the project,
> options with pros and cons, and a **recommendation**. Questions marked **Decided** are
> closed. Impact scale: **High** changes a response shape or a rule and so touches Core,
> Api, front end and tests; **Medium** touches two layers; **Low** is local to one file
> or screen.

### OQ-1. How does the tasks view know whether Close is allowed? (impact: High) — **Decided: A**

> Developer: "the return models should include those information." The list item and the
> grant detail both carry `pendingHighTasks` and `canClose`, from one `GrantRules.CanClose`
> rule. Built in steps B1.4, B4.2.

- **What:** ideal.md section 4 says the server decides the Close-disabled state, and the
  grants list gets `pendingHighTasks`. But the planned `GET /grants/{id}` returned only tasks and
  "today". The Close grant button in the tasks header therefore had no server-supplied answer.
- **Why it matters:** BR-5 is a graded rule. If the browser works it out from the task list
  (`priority === 'High' && status === 'Pending'`), the rule exists twice, and a change to BR-5
  (a likely live-change topic) must be made in two places and can disagree between the list and
  the detail. It also breaks the principle that the screen only reflects the rules.
- **How much it affects the project:** the detail response shape, the Core close rule (one method
  reused by list, detail and the close command), the Data queries, three test levels and the
  front-end Close button. Cheap now, expensive after the shapes are used.

| Option | Pros | Cons |
| --- | --- | --- |
| A. List **and** detail return `pendingHighTasks` and `canClose`, both from `GrantRules.CanClose` | One rule, same answer on both screens; the screen shows the reason from the count; no client logic | Two fields where one is derivable from the other |
| B. Detail returns `pendingHighTasks` only; the browser computes `canClose` from it and `status` | One extra field only | Close rule partly in the browser; a rule change touches both sides |
| C. The browser derives it from the task list | No API change | Rule duplicated; the exact thing ideal.md rejected |

**Recommendation: A.** `canClose` is the rule's answer and the count is its explanation.

### OQ-2. Which fields does the grant detail return? (impact: Medium) — **Decided: A**

> Developer reason: this enforces fetching the newest data, so what the officer reads is as
> fresh as possible. The detail is therefore a self-contained, fetched-on-open response and
> does not borrow the grant from the cached list. Built in steps B4.2, F5.1.

- **What:** the screen needs the reference and organisation (heading), `status` (read-only state
  and the Add, Complete and Edit reasons), the application date (date-picker minimum: later of
  `today` and application date), `today`, and the tasks. ideal.md listed only "grant, its tasks, and today".
- **Why it matters:** the date minimum is the only thing stopping an officer picking a date BR-2
  will refuse, and the "This grant is closed." state depends on `status`. If the shape is left
  implicit, it gets decided differently in the DTO, the front-end type and the tests.
- **How much it affects the project:** the detail DTO, the front-end `GrantDetailDto` type, the API
  body-shape tests and `backend.md` examples. Low effort if settled now.

| Option | Pros | Cons |
| --- | --- | --- |
| A. List the fields explicitly: `id`, `referenceNumber`, `organisationName`, `applicationDate`, `status`, `today`, `pendingHighTasks`, `canClose`, `tasks[]` | One agreed contract; the tasks view needs no data from the list | A little overlap with the list item |
| B. The tasks view reuses the grant from the (cached) list and the detail returns tasks and `today` only | Smaller detail | The tasks view then depends on the list cache; a refresh or sign-in swap with no list loaded breaks it; stale status |

**Recommendation: A.** The detail must stand on its own: it is the only source of `today`, and a
cached list entry can be older than the grant's current status.

### OQ-3. BR-2 and BR-3: beside the field or in the dialog? And what are the field names? (impact: Medium) — **Decided: A**

> Built in steps B1.2, F2.6, F6.1. ideal.md's sentence naming BR-2 as a dialog refusal is
> corrected when ideal.md is amended.

- **What:** ideal.md section 3 says "Field errors appear beside the field; API refusals (BR-1,
  BR-2) appear in the dialog", while sections 4 and 7 and the ARCHITECTURE.md error table make BR-2
  and BR-3 `400` validation errors with an `errors` map. The map keys also need fixing:
  `apiError.ts` lowercases them, so `dueDate` arrives as `duedate`.
- **Why it matters:** one rule shown two ways means the officer may see the date error under no
  field, or twice. Wrong keys mean the message never reaches the field.
- **How much it affects the project:** the `ValidationException` construction in Core, the dialog
  code and the API and component tests. A mismatch is a visible bug in the main demo flow (add a
  task with a past date).

| Option | Pros | Cons |
| --- | --- | --- |
| A. BR-2 and BR-3 are `400` with `errors` keyed `dueDate` and `title`, shown beside the field; only BR-1 (409) shows in the dialog | Matches the error contract and the check order (Q7); colour is not the only signal because the text sits at the field | Needs a documented key list |
| B. Show every refusal as one alert in the dialog | Simplest to build | Officer must map the message to the field; ignores the existing `fieldErrors` helper |
| C. Both: beside the field and a summary alert | Most accessible for screen readers | Duplicated text; more to build and test |

**Recommendation: A.** Document the keys in `backend.md`; correct the ideal.md sentence.

### OQ-4. What does `GET /grants?status=` do with an invalid value? (impact: Low) — **Decided: A**

> Built in step B4.3 (`ListGrantsHandler`) and tested in B4.6 and B6.4.

- **What:** `status` is `Open` or `Closed`, or omitted for all. `?status=Foo` or a wrong case had no
  defined behaviour.
- **Why it matters:** silently ignoring it returns all grants and hides a client bug; reviewers may
  probe the endpoint directly. It decides one API test and the error shown for a bad request.
- **How much it affects the project:** one query handler branch and one test.

| Option | Pros | Cons |
| --- | --- | --- |
| A. `400 VALIDATION_FAILED` with an `errors.status` entry; match case-insensitively | Honest; consistent with the error contract | One more validation path |
| B. Ignore invalid values and return all | No code | Hides bugs; surprising |
| C. `404` | None worth stating | Wrong meaning (the route exists) |

**Recommendation: A.**

### OQ-5. How stale may the counts, `canClose` and "today" be? (impact: Medium) — **Decided: A**

> Developer: keep it small, follow A, but comment and note the limit. Consequences: no polling.
> RTK Query refetches the grant detail when the tasks view or Add task dialog opens and on window
> focus (`refetchOnFocus`; `setupListeners` is already called in `store.ts`). The limit is written in
> four places: a code comment where the refetch options are set (F2.2), the README "Known limitations"
> (D3), section 9 above, and this entry. It states that data can be stale between those refetches
> (a High task added by another officer, or the business day passing midnight), that the server still
> decides (BR-2, BR-5), and that the screen then shows the refusal.

- **What:** the list's counts and `canClose`, and the detail's `today`, are read once and cached by
  RTK Query. Another officer can add a High task, or the business day can pass midnight, while the
  page stays open. ideal.md section 7 already accepts a stale Close state; "today" staleness was not mentioned.
- **Why it matters:** a stale `today` gives a wrong date-picker minimum and a wrong overdue badge. The
  server still decides (BR-2, BR-5), so data stays correct, but the officer sees a confusing refusal.
  Time-zone behaviour is a stated purpose of Q11.
- **How much it affects the project:** RTK Query options (`refetchOnFocus`, `refetchOnMountOrArgChange`,
  polling) and some component tests; no API change.

| Option | Pros | Cons |
| --- | --- | --- |
| A. Accept staleness; refetch the grant detail when the tasks view or Add task dialog opens, and refetch on window focus; the server still refuses | Cheap; keeps rules on the server; the date minimum is never older than the last open | A refusal can still appear after a race |
| B. A, plus periodic polling | Fresher | Extra requests; the retry policy and offline banner interact with polling |
| C. Accept staleness fully, no refetch | Nothing to build | Date minimum can be wrong for a long-open page |

**Recommendation: A.**

### OQ-6. Where do `IDEMPOTENCY_KEY_*`, 5xx and other non-inline errors appear? (impact: Low) — **Decided: A**

> Built in steps F2.5, F2.6, F6.1. `IDEMPOTENCY_KEY_MISMATCH` (422) shows in the dialog and is not
> toasted; `IDEMPOTENCY_KEY_REQUIRED` and `MALFORMED_REQUEST` keep their toast.

- **What:** the listener ignored 400, 404 and 409, shows unreachable errors in the banner and everything
  else as a toast named through `errorMessages.ts`. New endpoints need action phrases, and
  `IDEMPOTENCY_KEY_MISMATCH` (422) and `IDEMPOTENCY_KEY_REQUIRED` (400) were generic toasts shown over an open dialog.
- **Why it matters:** UI-STYLE section 7 says refused business actions appear inline at the action; a toast
  for a failure inside a dialog is easy to miss and disappears.
- **How much it affects the project:** `errorMessages.ts`, the dialog's error handling and two component
  tests. The 422 case is rare because the key is created once per intent.

| Option | Pros | Cons |
| --- | --- | --- |
| A. Add action phrases and code messages; also reset the key and show an alert in the dialog on 422 | Matches UI-STYLE; the officer can retry | Slightly more dialog logic |
| B. Toast only, as the listener does today | No work | Easy to miss inside a modal |

**Recommendation: A.**

### OQ-7. How does the API produce 412 and 428 in phase 2? (impact: Medium) — **Decided: A**

> Developer: `VersionConflictException` must explain to the user that the entity they want to update
> changed before their edit reached the database. Its message (which becomes the ProblemDetails `title`)
> reads: "This task was changed by someone else before your edit was saved. Reload to see the latest
> version, then edit again." The front-end dialog shows that text for a `412`. Built in steps E1, E2, E3.

- **What:** `AppExceptionHandler` maps only `ValidationException` (400), user (401), `NotFoundException`
  (404), `ConflictException` (409) and the idempotency mismatch (422). Q17 requires `412 VERSION_CONFLICT`
  and `428 PRECONDITION_REQUIRED`, and `EfUnitOfWork` must translate `DbUpdateConcurrencyException` so the
  audit behaviour records `Rejected`.
- **Why it matters:** the handler's switch decides every HTTP status; without a mapping both cases become
  500 and break the error contract the front end relies on. How it is built is the pattern future status
  codes follow, and `backend.md` must describe it.
- **How much it affects the project:** Core exceptions, the Api handler, the unit of work, tests at three
  levels, and the front-end listener. Only phase 2 (cuttable) is affected.

| Option | Pros | Cons |
| --- | --- | --- |
| A. Two new Core exception types (`VersionConflictException`, `PreconditionRequiredException`), each mapped in the handler | Same shape as existing exceptions; explicit and testable | Two more types and two switch arms |
| B. Give `AppException` an optional status and let the handler read it | One generic mechanism | HTTP concern leaks into Core; changes every existing exception |
| C. Reuse `ConflictException` and map by code | No new type | A 412 reported as a 409 breaks Q17 |

**Recommendation: A.**

### OQ-8. Are the list and detail responses unpaged? (impact: Low) — **Decided: A**

> Developer: no pagination for now, to keep this feature small. The README states the limit (D3).

- **What:** `GET /grants` returns all grants and `GET /grants/{id}` all tasks; the browser filters,
  searches and sorts tasks (ideal.md section 4).
- **Why it matters:** fine for the exercise (4 grants, 6 tasks) but a real portal grows; adding paging
  later changes response shapes and screens. A reviewer may ask about it.
- **How much it affects the project:** none now; later, the response shapes, the queries and the
  front-end filters.

| Option | Pros | Cons |
| --- | --- | --- |
| A. Unpaged, state the limit in the README | Matches the decision to filter in the browser; smallest | Does not scale beyond hundreds of rows |
| B. Add `page` and `pageSize` to `GET /grants` only | The list is the one that grows | Counts, sorting and the default Open filter need server work; more tests |
| C. Page both list and tasks | Complete | Contradicts browser-side filtering; large rework |

**Recommendation: A**, recorded as a known limitation, with B as a follow-up.
