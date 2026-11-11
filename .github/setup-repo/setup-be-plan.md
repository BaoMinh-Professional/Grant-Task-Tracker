# Setup repo: back-end base, implementation plan

> **Stage:** 2, implementation plan. **Status:** ✅ **Approved by the developer on
> 2026-11-11.** Implementation follows Phases 0–8. The agent makes no commits or pull
> requests (OQ-16).
> **Decisions:** OQ-1 to OQ-21 decided 2026-11-11 (section 13,
> [Open Questions](#13-open-questions); index in section 3.3).
> **Source of decisions:** [ideal.md](./ideal.md) (✅ green, 2026-11-11). Decisions
> recorded there are carried in as-is and are not re-argued here.
> **Companion doc:** [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) describes the
> resulting architecture (written with this plan at the developer's request; Phase 7
> reconciles it with the real code).

---

## 1. Goal and scope

### 1.1 Goal

Turn the empty `backend/` folders into a **base back end that builds, runs and
passes its tests**. It has every cross-cutting piece the six feature endpoints will
need, so each feature becomes "add one use-case folder, one controller action and
its tests".

### 1.2 In scope

| Area | What is delivered |
| --- | --- |
| Repository hygiene | `.gitignore`, `global.json` (SDK pin + MTP test runner), `Directory.Build.props`, `Directory.Packages.props` (central package versions), local `dotnet-ef` tool, removal of the stale `obj/` folders |
| Projects | `GrantTracker.slnx` with 3 source projects (`Api`, `Core`, `Data`) and 3 test projects (`Core.Tests`, `Data.Tests` (new), `Api.Tests`) and their references |
| Dependencies | Exact pinned versions (section 4) |
| Core | Hand-rolled mediator; pipeline behaviours for logging, audit (which also owns the command transaction) and idempotency; error types and codes; business clock; current-user abstraction; persistence ports; entities and enums for the whole data model; `Login` use case |
| Data | `DbContext`, entity configurations, UTC `DateTime` converter, unit of work (with unique-violation translation), repositories/stores for users, audit and idempotency, initial migration, seeder, database initialiser |
| Api | `Program.cs` pipeline, DI wiring, options and validation, Serilog (console + rolling JSON file), ProblemDetails (exception handler, status-code pages, model-state factory, `traceId` and `code`), current-user middleware, `[AllowAnonymousUser]` attribute, CORS, `AuthController` (`POST /auth/login`, OQ-11), `appsettings*.json`, `launchSettings.json`, `.http` file |
| Tests | Fixtures (SQLite in-memory database, API factory, fake clock) and the base test set (section 9) |
| Docs | `docs/ARCHITECTURE.md`, `docs/patterns/backend.md`, an `AGENTS.md` documentation map (which file holds what, and when to read it), and a new `README.md` describing the project and how to start it (OQ-6). `docs/ENTITIES.json` keeps its name and format as an illustration of the database design (OQ-5), with its content refreshed to the final model (OQ-21) |
| Validation | One final validation phase after all implementation (section 10), per the developer's instruction |

### 1.3 Out of scope (later feature plans)

- The six feature endpoints (list grants, get grant, add task, complete task, close
  grant, list overdue tasks) and business rules **BR-1 to BR-6**. The base only
  provides their entities, tables and seed data.
- **The assignment features** (OQ-20 B): show assignees, "assigned to me" filters,
  and assign/unassign commands with the rule "a task's assignee must be assigned to
  its grant" (`409`, `ASSIGNEE_NOT_ON_GRANT`). The base provides the two assignment
  tables, their constraints and a seed that already respects the rule.
- Grant/task repositories and their query tests (written with the features that use
  them).
- The end-to-end OP-3 duplicate-`POST` test. It needs `POST /grants/{id}/tasks`; the
  base covers the mechanism in `Core.Tests` and the unique key in `Data.Tests`.
- The front end, including the sign-in UI (Q15(b)), which moves to the front-end
  plan. The base provides `POST /auth/login`, the only endpoint it needs (OQ-11).
- `docs/patterns/frontend.md` (written with the front end).
- OP-1, OP-2, OP-5 and OP-6 (Q19), OpenAPI/Swagger, health checks, an idempotency
  cleanup job (Q12.6).
- Commits and pull requests: the developer makes them (OQ-16).

---

## 2. Design summary

### 2.1 What changes

```text
Project/
├─ .gitignore                         (new; dotnet template + SQLite/log entries)
├─ README.md                          (new; project description + how to start it, OQ-6)
├─ AGENTS.md                          (layout + documentation map, OQ-6)
├─ docs/
│  ├─ ARCHITECTURE.md                 (new)
│  ├─ ENTITIES.json                   (name and format kept, OQ-5; content refreshed, OQ-21)
│  └─ patterns/backend.md             (new)
└─ backend/
   ├─ global.json
   ├─ Directory.Build.props
   ├─ Directory.Packages.props
   ├─ .config/dotnet-tools.json       (dotnet-ef)
   ├─ GrantTracker.slnx
   ├─ src/
   │  ├─ GrantTracker.Api/            HTTP, ProblemDetails, middleware, DI wiring, config
   │  ├─ GrantTracker.Core/           entities, rules, use cases, mediator + behaviours, ports
   │  └─ GrantTracker.Data/           EF Core + SQLite, migrations, repositories, seed
   └─ tests/
      ├─ GrantTracker.Core.Tests/     unit: mediator, behaviours, clock, handlers (Moq)
      ├─ GrantTracker.Data.Tests/     integration: SQLite in-memory, constraints, seed
      └─ GrantTracker.Api.Tests/      HTTP end to end: WebApplicationFactory
```

### 2.2 Project references (dependency direction)

| Project | References | Must never reference |
| --- | --- | --- |
| `GrantTracker.Core` | none (only `Microsoft.Extensions.Logging.Abstractions`) | EF Core, ASP.NET Core, Serilog, `Data`, `Api` |
| `GrantTracker.Data` | `Core` | `Api`, ASP.NET Core |
| `GrantTracker.Api` | `Core`, `Data` | — |
| `GrantTracker.Core.Tests` | `Core` | `Data`, `Api` |
| `GrantTracker.Data.Tests` | `Data` (and `Core` transitively) | `Api` |
| `GrantTracker.Api.Tests` | `Api` (and the rest transitively) | — |

### 2.3 Request flow

1. **HTTP pipeline (Api):** exception handler → status-code pages → Serilog request
   logging → routing → CORS → current-user middleware → controller.
2. **Controller:** maps the request DTO to a command or query and calls
   `IMediator.Send`. It holds no business logic.
3. **Mediator pipeline (Core):** `LoggingBehaviour` → `AuditBehaviour` (commands only;
   owns the transaction) → `IdempotencyBehaviour` (only `IIdempotentCommand`) →
   handler.
4. **Handler (Core):** loads through ports, applies rules with `today` passed in,
   and changes entities. It never calls `SaveChanges`; the audit behaviour commits.
5. **Errors:** Core throws typed `AppException`s. The Api's exception handler maps
   them to ProblemDetails (section 2.5).

### 2.4 What stays as decided in `ideal.md`

Carried without change: Q1 B, Q2 C (with guardrails), Q3 B (one folder per use
case), Q4 (all), Q5 A, Q8 B, Q9 A, Q10 A, Q11 (reusable opt-in), Q12 B and points
3–6, Q13 C, Q14 A + D + E with U2, Q15 (a), (c), (d), (e), Q16 B, Q18 C, Q19 A, Q20 A,
Q21 A2 + B3 + C2.

### 2.5 Error contract (Q16) as implemented

| Exception (Core) | Status | `code` | Raised by |
| --- | --- | --- | --- |
| `ValidationException` | 400 (`ValidationProblemDetails` with `errors`) | `VALIDATION_FAILED`, `IDEMPOTENCY_KEY_REQUIRED`, later `BR-2`, `BR-3` | handlers, `IdempotencyBehaviour` |
| *(model binding, `[ApiController]`)* | 400 (`ValidationProblemDetails`) | `MALFORMED_REQUEST` | `InvalidModelStateResponseFactory` |
| `UserNotIdentifiedException` | 401 | `USER_REQUIRED` (header missing), `UNKNOWN_USER` (unparsable or unknown id, unknown email) | middleware (on every endpoint except login), `LoginHandler` |
| `NotFoundException` | 404 | `NOT_FOUND` | handlers |
| *(no endpoint matched)* | 404 | `NOT_FOUND` | status-code pages |
| `ConflictException` | 409 | later `BR-1`, `BR-5`, `ASSIGNEE_NOT_ON_GRANT` (OQ-20) | handlers |
| `IdempotencyKeyMismatchException` | 422 | `IDEMPOTENCY_KEY_MISMATCH` | `IdempotencyBehaviour` |
| anything else | 500 (generic `detail`, no stack trace) | `UNEXPECTED_ERROR` | exception handler (logged at `Error`) |

Every body also carries `traceId` = the **32-hex W3C trace id**
(`Activity.Current.TraceId`), the same value Serilog writes as `@tr`. ASP.NET Core's
default `traceId` is the full `traceparent` string (`00-…-…-00`), which doesn't match
the log field, so the plan overrides it (Q14 D).

---

## 3. Confirmed decisions

### 3.1 From `ideal.md` (summarised, not re-argued)

- **.NET 10**, ASP.NET Core Web API, **EF Core 10 + SQLite**; three source projects
  with Core free of EF/ASP.NET (Q1).
- **Hand-rolled mediator**: `Send` + ordered behaviours only; explicit DI
  registration; no scanning or notifications; tested itself (Q2).
- **CQRS-lite**: one folder per use case holding its command/query and handler (Q3).
- **Tests**: xUnit; Core.Tests (Moq on ports), Data.Tests (SQLite in-memory),
  Api.Tests (`WebApplicationFactory`, SQLite in-memory, `FakeTimeProvider`); one
  `dotnet test` (Q4, Q5).
- **Time**: `TimeProvider` only; moments in UTC (with a read converter); calendar
  dates as `DateOnly`; "today" in one configured business timezone (default `UTC`);
  no `DateTime.Now`/`Today`/`ToLocalTime()` (Q8).
- **Idempotency**: an opt-in `IIdempotentCommand` marker; a behaviour stores key,
  operation, SHA-256 request hash (excluding the key), the serialised result and
  `CreatedAtUtc` in the command's transaction; unique key; replay → stored result;
  mismatch → 422; successes only; no cleanup job (Q11–Q13).
- **Audit (U2)**: one row per command with `Outcome` = `Changed` / `NoChange` /
  `Replayed` / `Rejected` (+ rule code); success row in the same transaction,
  rejection row in a separate transaction after rollback; queries not audited (Q14).
- **Logging**: Serilog rolling JSON file; trace id on every line; user id,
  idempotency key and command name as properties (Q14).
- **Identity**: seeded `Users`; `POST /auth/login` (email only); stateless
  `X-User-Id` header resolved by middleware; every endpoint except
  `POST /auth/login` requires a known user; identification, not authentication (Q15).
- **Database lifecycle**: migrations applied at startup; seed only when empty
  (Q18).
- **Docs**: `docs/ARCHITECTURE.md` + separate back-end/front-end pattern docs, no
  diagrams, skeleton now and updated with each behaviour change (Q21).

### 3.2 Engineering details chosen in this plan

These follow directly from decisions in `ideal.md` and don't change scope, so they
are not open questions. Say if you want any of them changed.

| # | Detail | Why |
| --- | --- | --- |
| P4 | Pipeline behaviours are **open generics without type constraints**; each checks its marker at runtime (`request is ICommand`, `request is IIdempotentCommand`) and otherwise calls `next()` | MS DI's handling of constrained open generics inside `IEnumerable<>` is subtle; a runtime check is obvious and testable |
| P6 | Handlers report the audit target and outcome through a scoped **`IAuditContext`** (`SetTarget(entityType, entityId)`, `SetOutcome(...)`; default `Changed`) | Keeps response DTOs free of audit data; one line per handler |
| P8 | Request DTOs use **nullable properties and no data annotations**; Core validates | Rules live in one place (Core, Q1) and are unit-tested there; no duplicate, diverging validation |
| P9 | Serilog **sinks configured in code**: console always, rolling compact-JSON file when `LogFile:Path` is set; levels from the `Serilog` config section; `AddSerilog((services, lc) => …)` with **no bootstrap logger** | Tests switch the file off with one setting (array merging in config is index-based and fragile); a bootstrap logger breaks when several test hosts start in one process |
| P10 | **Configuration is read lazily** through `IServiceProvider`/`IOptions`, never eagerly from `builder.Configuration` in `Program.cs` | `WebApplicationFactory` applies test overrides at `Build()`, so eager reads would miss them |
| P11 | Connection string `Data Source=App_Data/grant-tracker.db`, **resolved against the content root**; folder created at startup; `*.db` and `App_Data/` git-ignored | Same file whether started from the repo root, `backend/` or the IDE; no credentials (SQLite) |
| P14 | **Enums stored as strings** in SQLite and JSON | Readable for support staff querying the database (OP-4); stable if enum members are reordered |
| P15 | **Central package management** (`Directory.Packages.props`), a shared `Directory.Build.props` (`net10.0`, nullable, implicit usings) and **`global.json`** (SDK pin + Microsoft Testing Platform for `dotnet test`) | One place to pin versions and shared settings; `global.json` is required by the test runner (OQ-13) |

### 3.3 Decisions on the open questions

Every question is written up in **section 13, Open Questions**, with its decision.

| # | Question | Recommendation | Decision |
| --- | --- | --- | --- |
| OQ-1 | New dependencies and tooling | Approve all three | ✅ **Approved, all** |
| OQ-2 | Assignment tables (`AssignUserTask`, `AssignUserGrant`) | Remove from the PoC | ✅ **C: keep and use them** |
| OQ-3 | How a task records "completed" | `CompletedAtUtc` only | ✅ **B: `Status` + `CompletedAtUtc`** |
| OQ-4 | How a grant records "closed" | `Status` enum (`Open`/`Closed`) | ✅ **C: `Status` + `ClosedAtUtc`** |
| OQ-5 | `ENTITIES.json` name and format (Q21 D) | Rename to `ENTITIES.dbml` | ✅ **Keep as is (illustration of the DB design)** |
| OQ-6 | Changes to `docs/`, `AGENTS.md` and a new `README.md` | All as planned | ✅ **A, with your wording for README and AGENTS.md** |
| OQ-7 | Custom MVC filters | None; one attribute | ✅ **A** |
| OQ-8 | Controllers or minimal APIs | Controllers | ✅ **A** |
| OQ-9 | Errors as exceptions or `Result<T>` | Typed exceptions | ✅ **A** (meets the judges' error-response requirement; see OQ-9) |
| OQ-10 | Mediator `Send` signature | Both type arguments explicit | ✅ **A** |
| OQ-11 | `GET /auth/me` and the sign-in UI (Q15(b)) | Add `/auth/me`; UI in the front-end plan | ✅ **B: no `/auth/me`**; UI in the front-end plan |
| OQ-12 | HTTPS and OpenAPI/Swagger in development | HTTP only, no OpenAPI, a `.http` file | ✅ **A** |
| OQ-13 | Test framework and runner | xunit.v3 4.x on Microsoft Testing Platform | ✅ **A** |
| OQ-14 | Who owns the command transaction | `AuditBehaviour` | ✅ **A** |
| OQ-15 | Front end to API: CORS or a Vite proxy | CORS from configuration | ✅ **A: CORS** |
| OQ-16 | Commit cadence with end-only validation | One commit per phase | ✅ **The agent doesn't commit or open PRs; you commit** |
| OQ-17 | Audit rows for unexpected (500) failures | None; logs only | ✅ **A** |
| OQ-18 | Seed data shape and fixed ids | Fixed readable ids + scenario seed | ✅ **A** |
| OQ-19 | Compiler warning policy | Nullable warnings as errors only | ✅ **A** |
| OQ-20 | **New (from OQ-2):** how assignments are used | Display + "assigned to me" filters; no permission rule yet | ✅ **B: display, filters and assign/unassign commands (feature plan)** |
| OQ-21 | **New (from OQ-5):** refresh `ENTITIES.json` content? | Refresh content, keep name and format | ✅ **A: refresh** |

---

## 4. Dependencies and tooling (new items need approval per `AGENTS.md`, see OQ-1)

Versions checked on nuget.org on 2026-11-11. Installed: SDK `10.0.302`, runtime
`10.0.10`. The `11.0.0` packages target .NET 11 and are deliberately not used.

| Package / tool | Version | Project(s) | Licence | Status |
| --- | --- | --- | --- | --- |
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.15 | Data | MIT | implied by the stack |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.15 | Api (`PrivateAssets=all`; the EF tools need it in the startup project) | MIT | **OQ-1** |
| `dotnet-ef` (local tool) | 10.0.15 | `backend/.config/dotnet-tools.json` | MIT | **OQ-1** |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.15 | Core (so behaviours can log without ASP.NET) | MIT | **OQ-1** |
| `Serilog.AspNetCore` | 10.0.0 | Api (brings Serilog 4.3, `Sinks.File` 7, `Sinks.Console`, `Formatting.Compact`, `Settings.Configuration`) | Apache-2.0 | approved (Q14 A) |
| `xunit.v3` | 4.0.3 | all test projects | Apache-2.0 | approved (Q4, version pinned here) |
| `Moq` | 4.21.0 (≥ 4.20.2, no SponsorLink) | Core.Tests | BSD-3-Clause | approved (Q4) |
| `Microsoft.Extensions.TimeProvider.Testing` | 10.11.0 | all test projects | MIT | approved (Q4) |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.15 | Api.Tests | MIT | approved (Q4) |

Not added: `Microsoft.AspNetCore.OpenApi` (template default, removed by
`--no-openapi`), `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, any mediator,
validation or mapping library.

---

## 5. Folder and file layout (target)

```text
backend/src/GrantTracker.Core/
  GrantTracker.Core.csproj
  Common/
    Mediator/      IRequest.cs (IRequest<T>, IQuery<T>, ICommand, ICommand<T>), IRequestHandler.cs,
                   IPipelineBehaviour.cs (+ RequestHandlerDelegate<T>), IMediator.cs, Mediator.cs
    Behaviours/    LoggingBehaviour.cs, AuditBehaviour.cs, IdempotencyBehaviour.cs
    Errors/        AppException.cs, ValidationException.cs, NotFoundException.cs, ConflictException.cs,
                   UserNotIdentifiedException.cs, IdempotencyKeyMismatchException.cs,
                   UniqueConstraintViolationException.cs, ErrorCodes.cs
    Time/          IBusinessClock.cs, BusinessClock.cs
    Identity/      ICurrentUser.cs
    Persistence/   IUnitOfWork.cs (+ IUnitOfWorkTransaction)
  Auditing/        AuditEntry.cs, AuditOutcome.cs, IAuditContext.cs, AuditContext.cs, IAuditLog.cs
  Idempotency/     IIdempotentCommand.cs, IdempotencyRecord.cs, IIdempotencyStore.cs, RequestHasher.cs
  Users/
    User.cs, UserDto.cs, IUserRepository.cs, EmailAddress.cs (Normalise)
    Login/         LoginQuery.cs, LoginHandler.cs
  Grants/          Grant.cs, GrantStatus.cs                (use-case folders come with features)
  Tasks/           GrantTask.cs, GrantTaskStatus.cs, TaskPriority.cs
                                                           (not `TaskStatus`: it clashes with System.Threading.Tasks)
  Assignments/     GrantAssignment.cs, TaskAssignment.cs   (use-case folders come with the assignment feature, OQ-20)

backend/src/GrantTracker.Data/
  GrantTracker.Data.csproj
  GrantTrackerDbContext.cs
  DataServiceCollectionExtensions.cs        AddGrantTrackerData(...)
  EfUnitOfWork.cs                           (+ EfUnitOfWorkTransaction)
  Configurations/  UserConfiguration.cs, GrantConfiguration.cs, GrantTaskConfiguration.cs,
                   GrantAssignmentConfiguration.cs, TaskAssignmentConfiguration.cs,
                   AuditEntryConfiguration.cs, IdempotencyRecordConfiguration.cs
  Conversions/     UtcDateTimeConverter.cs
  Repositories/    UserRepository.cs, AuditLog.cs, IdempotencyStore.cs
  Seeding/         DbSeeder.cs, SeedData.cs (fixed ids, names)
  DatabaseInitializer.cs
  Migrations/      <timestamp>_InitialCreate.cs, GrantTrackerDbContextModelSnapshot.cs (generated)

backend/src/GrantTracker.Api/
  GrantTracker.Api.csproj
  Program.cs
  GrantTracker.Api.http
  appsettings.json, appsettings.Development.json
  Properties/launchSettings.json
  Configuration/   BusinessTimeOptions.cs, LogFileOptions.cs, CorsSettings.cs, ConnectionStrings.cs (Resolve)
  DependencyInjection/ CoreServiceCollectionExtensions.cs (mediator, behaviours, handlers, clock)
  Logging/         SerilogConfiguration.cs
  Errors/          ProblemDetailsSetup.cs (CustomizeProblemDetails, InvalidModelStateResponseFactory),
                   AppExceptionHandler.cs (IExceptionHandler)
  Identity/        CurrentUserMiddleware.cs, HttpCurrentUser.cs, AllowAnonymousUserAttribute.cs, HeaderNames.cs
  Auth/            AuthController.cs, LoginRequest.cs

backend/tests/GrantTracker.Core.Tests/
  Common/Mediator/MediatorTests.cs
  Common/Behaviours/AuditBehaviourTests.cs, IdempotencyBehaviourTests.cs, LoggingBehaviourTests.cs
  Common/Time/BusinessClockTests.cs
  Idempotency/RequestHasherTests.cs
  Users/Login/LoginHandlerTests.cs
  TestDoubles/     TestCommand.cs, TestIdempotentCommand.cs, TestQuery.cs (+ handlers), TestClock.cs

backend/tests/GrantTracker.Data.Tests/
  Infrastructure/SqliteTestDatabase.cs
  MigrationTests.cs, UserRepositoryTests.cs, IdempotencyStoreTests.cs, AuditLogTests.cs,
  StatusConsistencyTests.cs, AssignmentConstraintTests.cs,
  UtcDateTimeConverterTests.cs, DateOnlyMappingTests.cs, DbSeederTests.cs

backend/tests/GrantTracker.Api.Tests/
  Infrastructure/GrantTrackerApiFactory.cs, ProblemDetailsAssertions.cs,
                 TestEndpointsController.cs (test-only routes, OQ-11)
  Auth/LoginTests.cs, CurrentUserTests.cs
  Errors/ErrorContractTests.cs
  Cors/CorsTests.cs
```

---

## 6. Data model (resolves Q7; OQ-2, OQ-3, OQ-4 decided)

All tables use `Guid` primary keys except `IdempotencyRecords`. Enums are stored as
text (P14). Moments are `DateTime` in UTC (`…AtUtc`), read back as
`DateTimeKind.Utc`. Calendar dates are `DateOnly` (`yyyy-MM-dd` text in SQLite).

| Table (entity) | Column | Type | Rules |
| --- | --- | --- | --- |
| `Users` (`User`) | `Id` | Guid | PK |
| | `Name` | text(100) | required |
| | `Email` | text(254) | required, **unique** (`IX_Users_Email`), stored lowercase (normalised on save and lookup) |
| `Grants` (`Grant`) | `Id` | Guid | PK |
| | `ReferenceNumber` | text(20) | required, **unique**, e.g. `GR-100001` |
| | `OrganisationName` | text(200) | required (was `FromOrg`) |
| | `ApplicationDate` | DateOnly | required (BR-2) |
| | `Status` | text(10) | `Open` / `Closed`; indexed (status filter) |
| | `ClosedAtUtc` | DateTime? | set exactly when `Status = Closed` (OQ-4 C) |
| `Tasks` (`GrantTask`) | `Id` | Guid | PK |
| | `GrantId` | Guid | required, FK → `Grants.Id`, `ON DELETE RESTRICT`, indexed |
| | `Title` | text(200) | required (BR-3 limits enforced in Core) |
| | `DueDate` | DateOnly | required |
| | `Priority` | text(10) | `Low` / `Medium` / `High` (BR-5) |
| | `Status` | text(10) | `Pending` / `Completed` (OQ-3 B); index `(Status, DueDate)` for the overdue query. "Overdue" is never stored: it depends on today |
| | `CompletedAtUtc` | DateTime? | set exactly when `Status = Completed`; never changed afterwards (BR-4) |
| | `CreatedAtUtc` | DateTime | required |
| `AssignUserGrant` (`GrantAssignment`) | `Id` | Guid | PK (as drafted) |
| | `UserId` | Guid | required, FK → `Users.Id`, `ON DELETE RESTRICT`, indexed ("my grants") |
| | `GrantId` | Guid | required, FK → `Grants.Id`, `ON DELETE RESTRICT` |
| | | | **unique `(GrantId, UserId)`**: a user is assigned to a grant at most once |
| `AssignUserTask` (`TaskAssignment`) | `Id` | Guid | PK (as drafted) |
| | `UserId` | Guid | required, FK → `Users.Id`, `ON DELETE RESTRICT`, indexed ("my tasks") |
| | `TaskId` | Guid | required, FK → `Tasks.Id`, `ON DELETE RESTRICT` |
| | | | **unique `(TaskId, UserId)`** |
| `Audits` (`AuditEntry`) | `Id` | Guid | PK |
| | `OccurredAtUtc` | DateTime | required, indexed |
| | `UserId` | Guid | required, FK → `Users.Id`, `ON DELETE RESTRICT` |
| | `Action` | text(100) | command name without the `Command` suffix, e.g. `CloseGrant` |
| | `EntityType` | text(50)? | e.g. `Grant`, `Task`; index `(EntityType, EntityId)` |
| | `EntityId` | Guid? | |
| | `Outcome` | text(20) | `Changed` / `NoChange` / `Replayed` / `Rejected` |
| | `ErrorCode` | text(50)? | set when `Rejected`, e.g. `BR-5` |
| | `TraceId` | text(32)? | W3C trace id (links to logs) |
| | `IdempotencyKey` | text(100)? | when the command carried one |
| `IdempotencyRecords` (`IdempotencyRecord`) | `Key` | text(100) | **PK** (the unique index for concurrent retries) |
| | `Operation` | text(100) | command type name |
| | `RequestHash` | text(64) | SHA-256 hex of the command without the key |
| | `ResultJson` | text | serialised handler result |
| | `CreatedAtUtc` | DateTime | required (TTL documented, no cleanup yet) |

**Keeping status and timestamp consistent (OQ-3 B, OQ-4 C).** Two columns describe
one fact, so three guards keep them in step:

1. **Database check constraints** (SQLite evaluates comparisons as 0/1):
   - `CK_Tasks_Status_CompletedAt`: `("Status" = 'Completed') = ("CompletedAtUtc" IS NOT NULL)`
   - `CK_Grants_Status_ClosedAt`: `("Status" = 'Closed') = ("ClosedAtUtc" IS NOT NULL)`
2. **Entities** only change them together. `Status` and the timestamp have private
   setters; the feature methods (`GrantTask.Complete(utcNow)`, `Grant.Close(utcNow)`)
   set both in one place. The base adds only constructors that accept a consistent pair.
3. **Tests** in `Data.Tests` prove each constraint rejects an inconsistent row.

**Assignments (OQ-2 C).** Both link tables are kept as drafted, plus the unique pairs
above. The draft rule "a task's assignee should be assigned to the task's grant" is
**not** a database constraint (SQLite can't express it simply). It is a Core rule
enforced by the assignment commands (OQ-20 B, feature plan), and the seed respects it.

Changed from the draft: `Grants` gains `ReferenceNumber`, `ClosedAtUtc` and
`OrganisationName` (was `FromOrg`); `Tasks` gains `Priority`, `CompletedAtUtc` and
`CreatedAtUtc`; `Audits` is reshaped for U2 (`ById`/`Timestamp`/`Description`
replaced by the columns above); `IdempotencyRecords` is new.

Entities expose `{ get; private set; }` properties and constructors for required
values (EF uses constructor binding / private setters). `Grant` has `Tasks` and
`Assignments` navigations; `GrantTask` has `Assignments`. Behaviour methods
(`Complete`, `Close`, `Assign`, …) arrive with the features that need them.

### 6.1 Seed data (only when the `Users` table is empty)

`T` = business "today" from `IBusinessClock` at startup; `now` = `IBusinessClock.UtcNow`.

**Users** (fictional, `example.org`, fixed ids):

| Id | Name | Email |
| --- | --- | --- |
| `00000000-0000-0000-0000-000000000001` | Alex Nguyen | `alex.nguyen@example.org` |
| `00000000-0000-0000-0000-000000000002` | Priya Sharma | `priya.sharma@example.org` |
| `00000000-0000-0000-0000-000000000003` | Sam Taylor | `sam.taylor@example.org` |
| `00000000-0000-0000-0000-000000000004` | Jordan Lee | `jordan.lee@example.org` |
| `00000000-0000-0000-0000-000000000005` | Maria Garcia | `maria.garcia@example.org` |

**Grants and tasks** (chosen so every rule can be demonstrated; a completed task has
`Status = Completed` and its `CompletedAtUtc`; the closed grant has `ClosedAtUtc`):

| Grant | Organisation | Applied | Status | Assigned officers | Tasks (title, priority, due, completed, assignee) | Demonstrates |
| --- | --- | --- | --- | --- | --- | --- |
| `GR-100001` | Riverside Community Garden Inc. | T−60 | Open | Alex, Priya | Verify bank details, High, T−10, pending, Alex · Review budget, Medium, T−3, pending, Priya · Send acknowledgement letter, Low, T−20, completed `now−18d`, Alex | overdue ordering (10 > 3 days), BR-5 blocks close, two officers on one grant |
| `GR-100002` | Northside Youth Sports Club | T−30 | Open | Sam, Alex | Schedule site visit, Medium, T, pending, Sam · Check eligibility, Low, T−1, pending, Sam · Collect insurance certificate, High, T+7, pending, Alex | due today is *not* overdue; overdue by 1 day |
| `GR-100003` | Harbour Seniors Network | T−14 | Open | Jordan | Initial eligibility check, High, T−7, completed `now−8d`, Jordan · Confirm contact details, Low, T+14, pending, *unassigned* | close allowed (no pending High); an unassigned task |
| `GR-100004` | Eastside Arts Collective | T−120 | Closed (`ClosedAtUtc = now−25d`) | Maria | Final report received, Medium, T−30, completed `now−32d`, Maria | BR-1 (closed grant), status filter |
| `GR-100005` | Westfield Food Bank | T−5 | Open | *none* | none | zero pending count; an unassigned grant |

Every task assignee is also assigned to that task's grant (the OQ-20 rule).

---

## 7. Key component designs

### 7.1 Mediator (Core, about 30 lines)

```csharp
public interface IRequest<TResponse> { }
public interface IQuery<TResponse> : IRequest<TResponse> { }
public interface ICommand { }                                    // marker used by AuditBehaviour
public interface ICommand<TResponse> : IRequest<TResponse>, ICommand { }

public interface IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

public interface IPipelineBehaviour<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

public interface IMediator
{
    Task<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResponse>;
}

public sealed class Mediator(IServiceProvider services) : IMediator
{
    public Task<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResponse>
    {
        var handler = services.GetService(typeof(IRequestHandler<TRequest, TResponse>)) as IRequestHandler<TRequest, TResponse>
            ?? throw new InvalidOperationException($"No handler registered for {typeof(TRequest).Name}.");
        var behaviours = services.GetService(typeof(IEnumerable<IPipelineBehaviour<TRequest, TResponse>>))
            as IEnumerable<IPipelineBehaviour<TRequest, TResponse>> ?? [];

        RequestHandlerDelegate<TResponse> next = () => handler.Handle(request, cancellationToken);
        foreach (var behaviour in behaviours.Reverse())   // first registered = outermost
        {
            var inner = next;
            next = () => behaviour.Handle(request, inner, cancellationToken);
        }
        return next();
    }
}
```

Registration (Api, `CoreServiceCollectionExtensions.AddGrantTrackerCore`), all
scoped, **in this order**:

```csharp
services.AddScoped<IMediator, Mediator>();
services.AddScoped(typeof(IPipelineBehaviour<,>), typeof(LoggingBehaviour<,>));
services.AddScoped(typeof(IPipelineBehaviour<,>), typeof(AuditBehaviour<,>));
services.AddScoped(typeof(IPipelineBehaviour<,>), typeof(IdempotencyBehaviour<,>));
services.AddScoped<IAuditContext, AuditContext>();
// Use cases: one explicit line each (the place to add a feature)
services.AddScoped<IRequestHandler<LoginQuery, UserDto>, LoginHandler>();
```

### 7.2 `LoggingBehaviour` (all requests)

- Opens `logger.BeginScope({ Request = typeof(TRequest).Name, UserId, IdempotencyKey })`.
  Serilog turns the scope into properties on every line inside it.
- On success, logs `Handled {Request} in {ElapsedMs} ms` (Information).
- On `AppException`, logs `Rejected {Request} with {ErrorCode}` (Warning) and
  rethrows.
- Other exceptions are rethrown without logging (the Api exception handler logs
  them once at Error).

### 7.3 `AuditBehaviour` (only `ICommand`; owns the transaction)

```text
if request is not ICommand → return await next()
userId = currentUser.UserId ?? throw InvalidOperationException ("commands require an identified user")
try
    await using tx = unitOfWork.BeginTransactionAsync()
    result = await next()                                   // idempotency + handler (no SaveChanges in handlers)
    auditLog.Add(entry(outcome: auditContext.Outcome, errorCode: null))
    await unitOfWork.SaveChangesAsync()
    await tx.CommitAsync()
    return result
catch AppException ex                                       // the transaction is already disposed, so rolled back
    unitOfWork.DiscardChanges()                             // ChangeTracker.Clear()
    auditLog.Add(entry(outcome: Rejected, errorCode: ex.Code))
    try await unitOfWork.SaveChangesAsync()                 // its own implicit transaction
    catch Exception auditEx → logger.LogError(auditEx, …)   // never hide the original error; logs are the fallback
    throw
```

The entry contains `OccurredAtUtc = clock.UtcNow`, `UserId`, `Action` (type name
minus `Command`), target from `IAuditContext`, `TraceId =
Activity.Current?.TraceId.ToHexString()`, and `IdempotencyKey` when present.
Unexpected (non-`AppException`) errors roll back and write **no** audit row; they
are logged at Error with the trace id (recorded as a known limitation).

### 7.4 `IdempotencyBehaviour` (only `IIdempotentCommand`)

```text
if request is not IIdempotentCommand c → return await next()
key = c.IdempotencyKey?.Trim()
if key is empty or longer than 100 → throw ValidationException(IDEMPOTENCY_KEY_REQUIRED, field "Idempotency-Key")
operation = typeof(TRequest).Name;  hash = RequestHasher.Hash(request)
existing = await store.FindAsync(key)
if existing != null → return Replay(existing)
result = await next()
store.Add(new IdempotencyRecord(key, operation, hash, JsonSerializer.Serialize(result), clock.UtcNow))
try   await unitOfWork.SaveChangesAsync()               // flush inside the audit transaction; EF uses a savepoint
catch UniqueConstraintViolationException                 // a concurrent retry won the race
    unitOfWork.DiscardChanges()
    existing = await store.FindAsync(key) ?? throw
    return Replay(existing)
return result

Replay(existing):
    if existing.Operation != operation or existing.RequestHash != hash → throw IdempotencyKeyMismatchException (422)
    auditContext.SetOutcome(Replayed)
    return JsonSerializer.Deserialize<TResponse>(existing.ResultJson)
```

- `IIdempotentCommand` has `string? IdempotencyKey { get; }`. The controller copies
  `[FromHeader(Name = "Idempotency-Key")]` into the command (Q13 C).
- `RequestHasher` serialises the command with `System.Text.Json` (declaration order
  is fixed per type) using a `DefaultJsonTypeInfoResolver` modifier that **drops the
  `IdempotencyKey` property**, then takes SHA-256 as lowercase hex. No per-command
  attribute is needed.
- `UniqueConstraintViolationException` is a Core type. `EfUnitOfWork` translates
  `DbUpdateException` whose inner `SqliteException` has error code 19 and extended
  code 2067 (UNIQUE) or 1555 (PRIMARY KEY), so Core never sees EF types.

### 7.5 Clock (Core)

```csharp
public interface IBusinessClock { DateTime UtcNow { get; } DateOnly Today { get; } }

public sealed class BusinessClock(TimeProvider timeProvider, TimeZoneInfo businessTimeZone) : IBusinessClock
{
    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), businessTimeZone).DateTime);
}
```

Api registers `TimeProvider.System` (singleton), binds `BusinessTime:TimeZoneId`
(IANA, default `UTC`) with `ValidateOnStart` (unknown id → startup failure), and
registers `IBusinessClock` as a singleton built from both. Tests replace
`TimeProvider` with `FakeTimeProvider`.

### 7.6 Identity (Api + Core)

- `ICurrentUser` (Core): `Guid? UserId`, `string? Name`.
- `HttpCurrentUser` (Api, scoped) implements it and has `Set(User user)`.
- `[AllowAnonymousUser]` (Api): an attribute (class/method), read as endpoint
  metadata. Only `AuthController.Login` carries it.
- `CurrentUserMiddleware` (after routing and CORS):
  1. No endpoint matched, or the endpoint has `AllowAnonymousUserAttribute` → `next()`.
  2. Header `X-User-Id` missing or blank → `UserNotIdentifiedException(USER_REQUIRED)`.
  3. Not a GUID, or `IUserRepository.FindByIdAsync` returns null →
     `UserNotIdentifiedException(UNKNOWN_USER)`.
  4. Otherwise `HttpCurrentUser.Set(user)`, `IDiagnosticContext.Set("UserId", id)` (request
     summary line), `LogContext.PushProperty("UserId", id)` for the rest of the
     request, then `next()`.
- `LoginHandler` (Core, `LoginQuery(string? Email) : IQuery<UserDto>`): blank →
  `ValidationException(VALIDATION_FAILED, "email")`; `EmailAddress.Normalise`
  (trim + lowercase invariant); unknown → `UserNotIdentifiedException(UNKNOWN_USER)`.
  It is a query, so it is not audited (there is no user yet); attempts are logged.

### 7.7 Api endpoints in the base

| Method + route | User required | Body / headers | Success | Errors |
| --- | --- | --- | --- | --- |
| `POST /auth/login` | no (`[AllowAnonymousUser]`) | `{ "email": "…" }` | `200 { id, name, email }` | 400 `VALIDATION_FAILED` / `MALFORMED_REQUEST`, 401 `UNKNOWN_USER` |

This is the only production endpoint in the base (OQ-11). The current-user check is
**on by default**: every endpoint added later requires `X-User-Id` unless it carries
`[AllowAnonymousUser]`. Until the first feature endpoint exists, the check is proven by
a test-only controller in `Api.Tests` (section 9.3).

### 7.8 `Program.cs` (order matters)

```text
builder.Services:
  AddSerilog((sp, lc) => SerilogConfiguration.Configure(sp, lc))       // P9
  AddProblemDetails(ProblemDetailsSetup.Customize)                     // traceId (32-hex) + default code by status
  AddExceptionHandler<AppExceptionHandler>()
  AddControllers().AddJsonOptions(JsonStringEnumConverter)
      .ConfigureApiBehaviorOptions(InvalidModelStateResponseFactory → MALFORMED_REQUEST)
  AddCors(policy from CorsSettings, headers X-User-Id + Idempotency-Key + Content-Type)
  AddOptions<BusinessTimeOptions>().Bind(...).Validate(zone exists).ValidateOnStart()
  AddSingleton(TimeProvider.System); AddSingleton<IBusinessClock>(...)
  AddScoped<HttpCurrentUser>(); AddScoped<ICurrentUser>(sp => sp.GetRequiredService<HttpCurrentUser>())
  AddGrantTrackerCore()                                                // mediator, behaviours, handlers
  AddGrantTrackerData(sp => ConnectionStrings.Resolve(sp))             // DbContext, UoW, repositories, seeder, initialiser

app:
  await DatabaseInitializer (scope): create App_Data folder → MigrateAsync → seed if empty
  UseExceptionHandler()
  UseStatusCodePages()
  UseSerilogRequestLogging()
  UseRouting()
  UseCors()
  UseMiddleware<CurrentUserMiddleware>()
  MapControllers()
  Run()

public partial class Program;                                         // for WebApplicationFactory
```

### 7.9 Configuration files

`appsettings.json`:

```json
{
  "ConnectionStrings": { "GrantTracker": "Data Source=App_Data/grant-tracker.db" },
  "BusinessTime": { "TimeZoneId": "UTC" },
  "Cors": { "AllowedOrigins": [ "http://localhost:5173" ] },
  "LogFile": { "Path": "logs/grant-tracker-.json", "RetainedFileCountLimit": 14 },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  },
  "AllowedHosts": "*"
}
```

- `appsettings.Development.json`: `Serilog:MinimumLevel:Default = Debug` only.
- `LogFile:Path` is relative to the content root; daily rolling, compact JSON
  (`@t`, `@mt`, `@tr`, `@sp`, properties).
- `launchSettings.json`: an `http` profile (`http://localhost:5080`, the default) and,
  added after implementation at the developer's request, an opt-in `https` profile
  (`https://localhost:7080` plus `http://localhost:5080`, no HTTPS redirection; see the
  OQ-12 amendment). Both use `ASPNETCORE_ENVIRONMENT=Development` and `launchBrowser: false`.
- No secrets anywhere; the SQLite connection string has no credentials.

---

## 8. Phased work breakdown

> Tick the boxes as work completes; this list is the single source of truth for
> progress. All commands run from `backend/` unless stated. **No build/test
> validation runs between phases** (developer instruction); Phase 8 validates
> everything. The only compile happens inside `dotnet ef migrations add` (Phase 4),
> because EF needs it to generate the migration.

### Phase 0: Repository hygiene

- [x] 0.1 Delete the stale `obj/` folders under `backend/src/*` and `backend/tests/*`.
- [x] 0.2 Repo root: `dotnet new gitignore`, then append `*.db`, `*.db-shm`, `*.db-wal`,
      `**/App_Data/`, `**/logs/`.
- [x] 0.3 `backend/global.json`:
      ```json
      {
        "sdk": { "version": "10.0.302", "rollForward": "latestFeature" },
        "test": { "runner": "Microsoft.Testing.Platform" }
      }
      ```
- [x] 0.4 `backend/Directory.Build.props`: `TargetFramework=net10.0`, `Nullable=enable`,
      `ImplicitUsings=enable`, `LangVersion=latest`, `WarningsAsErrors=nullable`.
- [x] 0.5 `backend/Directory.Packages.props`: `ManagePackageVersionsCentrally=true` and
      one `<PackageVersion>` per package in section 4.
- [x] 0.6 `dotnet new tool-manifest` then `dotnet tool install dotnet-ef --version 10.0.15`.
- 0.7 *Suggested commit point (you commit):* `chore(backend): add repo hygiene, SDK pin and central package versions`.

### Phase 1: Solution and projects

- [x] 1.1 `dotnet new sln -n GrantTracker` (creates `GrantTracker.slnx`).
- [x] 1.2 `dotnet new classlib -n GrantTracker.Core -o src/GrantTracker.Core` and the
      same for `GrantTracker.Data`; delete `Class1.cs`.
- [x] 1.3 `dotnet new webapi -n GrantTracker.Api -o src/GrantTracker.Api --use-controllers --no-openapi --no-https -au None`;
      delete `WeatherForecast.cs`, `Controllers/WeatherForecastController.cs` and the template `.http` content.
- [x] 1.4 Create `tests/GrantTracker.Core.Tests`, `tests/GrantTracker.Data.Tests` and
      `tests/GrantTracker.Api.Tests` as SDK-style projects (no template, to avoid
      xunit v2 packages):
      ```xml
      <Project Sdk="Microsoft.NET.Sdk">
        <PropertyGroup>
          <OutputType>Exe</OutputType>
          <IsPackable>false</IsPackable>
          <IsTestProject>true</IsTestProject>
          <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
        </PropertyGroup>
        <ItemGroup>
          <PackageReference Include="xunit.v3" />
          <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />
          <!-- Core.Tests: Moq; Api.Tests: Microsoft.AspNetCore.Mvc.Testing -->
        </ItemGroup>
        <ItemGroup><Using Include="Xunit" /></ItemGroup>
      </Project>
      ```
- [x] 1.5 Strip `Version=` attributes from template-generated package references
      (versions live in `Directory.Packages.props`) and remove properties duplicated by
      `Directory.Build.props`.
- [x] 1.6 Add the projects to the solution with `dotnet sln add` (6 projects).
- [x] 1.7 Project references exactly as section 2.2 (`dotnet add reference`).
- [x] 1.8 Package references exactly as section 4 (`dotnet add package`, no version).
- 1.9 *Suggested commit point (you commit):* `chore(backend): scaffold solution, source and test projects`.

### Phase 2: Core

- [x] 2.1 `Common/Mediator`: contracts and `Mediator` (section 7.1).
- [x] 2.2 `Common/Errors`: `AppException(string code, string message)` (abstract) and
      subclasses; `ValidationException` carries `IReadOnlyDictionary<string, string[]> Errors`;
      `ErrorCodes` constants (`VALIDATION_FAILED`, `MALFORMED_REQUEST`, `USER_REQUIRED`,
      `UNKNOWN_USER`, `NOT_FOUND`, `IDEMPOTENCY_KEY_REQUIRED`, `IDEMPOTENCY_KEY_MISMATCH`,
      `UNEXPECTED_ERROR`); `UniqueConstraintViolationException` (not an `AppException`).
- [x] 2.3 `Common/Time`: `IBusinessClock`, `BusinessClock` (section 7.5).
- [x] 2.4 `Common/Identity/ICurrentUser`; `Common/Persistence/IUnitOfWork`
      (`BeginTransactionAsync`, `SaveChangesAsync`, `DiscardChanges`) and
      `IUnitOfWorkTransaction` (`CommitAsync`, `IAsyncDisposable`; disposing without a
      commit rolls back).
- [x] 2.5 Entities and enums: `User`, `Grant` + `GrantStatus`, `GrantTask` +
      `GrantTaskStatus` + `TaskPriority`, `GrantAssignment`, `TaskAssignment`,
      `AuditEntry` + `AuditOutcome`, `IdempotencyRecord` (section 6). Constructors only
      accept a consistent status/timestamp pair (throw `ArgumentException` otherwise).
- [x] 2.6 `Auditing`: `IAuditContext`/`AuditContext` (scoped; `SetTarget`, `SetOutcome`,
      default `Changed`), `IAuditLog` (`Add`).
- [x] 2.7 `Idempotency`: `IIdempotentCommand`, `IIdempotencyStore` (`FindAsync`, `Add`),
      `RequestHasher`.
- [x] 2.8 `Common/Behaviours`: `LoggingBehaviour`, `AuditBehaviour`,
      `IdempotencyBehaviour` (sections 7.2–7.4). No `DateTime.Now`; use `IBusinessClock`.
- [x] 2.9 `Users`: `UserDto(Guid Id, string Name, string Email)`, `IUserRepository`
      (`FindByIdAsync`, `FindByEmailAsync`), `EmailAddress.Normalise`,
      `Login/LoginQuery` + `LoginHandler` (section 7.6).
- 2.10 *Suggested commit point (you commit):* `feat(core): add mediator, behaviours, errors, clock and domain model`.

### Phase 3: Data

- [x] 3.1 `GrantTrackerDbContext` with `DbSet`s for the seven entities;
      `ApplyConfigurationsFromAssembly`; `ConfigureConventions`:
      `Properties<DateTime>().HaveConversion<UtcDateTimeConverter>()` and the nullable
      equivalent.
- [x] 3.2 `UtcDateTimeConverter`: write `Unspecified` as is and `Local` via
      `ToUniversalTime()`; read with `DateTime.SpecifyKind(v, DateTimeKind.Utc)`.
- [x] 3.3 Entity configurations (section 6): table names (`AssignUserGrant`,
      `AssignUserTask` as drafted), keys, lengths, required, unique indexes (including
      the two assignment pairs), FKs with `DeleteBehavior.Restrict`, enums
      `HasConversion<string>()`, navigations, and the two check constraints via
      `ToTable(t => t.HasCheckConstraint(...))`.
- [x] 3.4 `EfUnitOfWork` (+ transaction wrapper) translating SQLite constraint failures
      (section 7.4): unique/primary key → `UniqueConstraintViolationException`; a CHECK
      failure (extended code 275) is left as is, because it means a bug, not a user error.
- [x] 3.5 `UserRepository` (email compared after `EmailAddress.Normalise`), `AuditLog`,
      `IdempotencyStore`.
- [ ] 3.6 `Seeding/SeedData` (fixed ids, names) and `DbSeeder.SeedAsync` (section 6.1,
      including assignments; returns early when `Users` has rows; one `SaveChangesAsync`).
- [x] 3.7 `DatabaseInitializer.InitialiseAsync`: when the data source is a file (not
      `:memory:` and not `Mode=Memory`), create its folder; `MigrateAsync`; seed; log
      the outcome.
- [x] 3.8 `DataServiceCollectionExtensions.AddGrantTrackerData(Func<IServiceProvider, string> connectionString)`:
      `AddDbContext` (`UseSqlite`), `IUnitOfWork`, `IUserRepository`, `IAuditLog`,
      `IIdempotencyStore` (scoped), `DbSeeder`, `DatabaseInitializer`.
- 3.9 *Suggested commit point (you commit):* `feat(data): add DbContext, configurations, repositories and seeding`.

### Phase 4: Api

- [x] 4.1 `Configuration/*` options and `ConnectionStrings.Resolve` (relative data source →
      absolute under `IHostEnvironment.ContentRootPath`; memory sources untouched).
- [x] 4.2 `Logging/SerilogConfiguration` (P9): `ReadFrom.Configuration`,
      `ReadFrom.Services`, `Enrich.FromLogContext`, console sink, file sink
      (`CompactJsonFormatter`, `RollingInterval.Day`, `retainedFileCountLimit`) when
      `LogFile:Path` is non-empty.
- [x] 4.3 `Errors/ProblemDetailsSetup` and `Errors/AppExceptionHandler` (section 2.5).
- [x] 4.4 `Identity/*` (section 7.6).
- [x] 4.5 `DependencyInjection/CoreServiceCollectionExtensions.AddGrantTrackerCore` (section 7.1).
- [x] 4.6 `Auth/AuthController` (`[ApiController] [Route("auth")]`), `LoginRequest(string? Email)`.
- [x] 4.7 `Program.cs` (section 7.8), `appsettings*.json`, `launchSettings.json` (section 7.9).
- [x] 4.8 `GrantTracker.Api.http`: login as Alex (mixed-case email); login with an
      unknown email (401); login with a blank email (400); an unknown route (404).
      Seeded user ids listed in a comment for later endpoints.
- [x] 4.9 Generate the migration:
      `dotnet ef migrations add InitialCreate --project src/GrantTracker.Data --startup-project src/GrantTracker.Api --output-dir Migrations`.
- 4.10 *Suggested commit point (you commit):* `feat(api): add pipeline, ProblemDetails, identity, logging and initial migration`.

### Phase 5: Tests

- [ ] 5.1 Shared fixtures:
  - `Data.Tests/Infrastructure/SqliteTestDatabase`: opens `DataSource=:memory:`,
    builds options on that connection, `MigrateAsync()`, `CreateContext()`; disposes
    the connection. One per test.
  - `Api.Tests/Infrastructure/GrantTrackerApiFactory : WebApplicationFactory<Program>`:
    environment `Testing`; `ConnectionStrings:GrantTracker = Data Source=api-tests-{Guid:N};Mode=Memory;Cache=Shared`
    with a keep-alive connection opened before the host starts; `LogFile:Path = ""`;
    `FakeTimeProvider` (2026-03-10T09:00Z) replacing `TimeProvider`; exposes `Clock`
    and `CreateClient(userId?)` that sets `X-User-Id`. One factory per test (order
    independent). It also registers a **test-only controller**
    (`Infrastructure/TestEndpointsController`, added with
    `AddControllers().AddApplicationPart(...)` in `ConfigureTestServices`):
    `GET /__test/whoami` (requires a user; returns `ICurrentUser`) and
    `GET /__test/throw` (requires a user; throws `InvalidOperationException`). It lives
    only in the test assembly, so production has no such routes (OQ-11).
  - `Core.Tests/TestDoubles`: test command/query types and handlers.
- [ ] 5.2 Write the tests listed in section 9.
- 5.3 *Suggested commit point (you commit):* `test(backend): add Core, Data and Api test projects for the base`.

### Phase 6: README and AGENTS.md (OQ-6)

- [ ] 6.1 `README.md` (repo root, new) **describes the project and how to start it**:
  - what the project is (the grants portal, the PoC scope, the tech stack);
  - prerequisites (.NET SDK 10.0.302+);
  - how to start it: `cd backend`, `dotnet tool restore`,
    `dotnet run --project src/GrantTracker.Api` → `http://localhost:5080`, then try the
    calls in `GrantTracker.Api.http` (seeded users and their ids);
  - how to run the tests (`dotnet test` from `backend/`);
  - how to reset the demo database (stop the API, delete `App_Data/grant-tracker.db*`,
    start again) and where the logs are;
  - short notes that `ideal.md` requires the README to carry: "identification, not
    authentication" (Q15), the idempotency TTL and cleanup next step (Q12.6),
    "migrations would run as a deploy step in production" (Q18), and the Moq version
    note (Q4).
  - A front-end section is added by the front-end plan.
- [ ] 6.2 `AGENTS.md`: replace "Backend and frontend folders: to be added" with the
      `backend/` layout, and add a **documentation map**: which file holds which
      information, and when to read it:

      | File | Holds | Read when |
      | --- | --- | --- |
      | `README.md` | What the project is; how to start it, test it and reset the database | First time in the repo; running the app |
      | `prompt-style.md` | The workflow: idea → approval → plan → approval → code | Before starting any feature |
      | `.github/<feature>/ideal.md` | A feature's decision record (options, trade-offs, why) | Before changing anything that feature decided; when asked "why" |
      | `.github/<feature>/*plan*.md` | A feature's implementation steps and progress checkboxes | While implementing that feature |
      | `docs/ARCHITECTURE.md` | Layers, project references, request flow, error contract, cross-cutting concerns | Before structural changes; when unsure where code belongs |
      | `docs/patterns/backend.md` | Recipes: add a use case, rule, endpoint or test; idempotency; audit; clock | Before writing back-end code |
      | `docs/patterns/frontend.md` (later) | The RTK Query boilerplate | Before writing front-end code |
      | `docs/ENTITIES.json` | Illustration of the database design (DBML syntax), kept in sync with the schema; the EF configurations and migrations are the real schema | When changing entities or migrations; when explaining the data model |
- 6.3 *Suggested commit point (you commit):* `docs: add README and AGENTS.md documentation map`.

### Phase 7: Architecture and pattern docs (approved `docs/` changes)

- [ ] 7.1 Reconcile [docs/ARCHITECTURE.md](../../docs/ARCHITECTURE.md) with the code
      as built (names, order, file paths); change its status line to "matches code".
- [ ] 7.2 Create `docs/patterns/backend.md`: how to add a use case (folder, command or
      query, handler, `IAuditContext` calls, DI line, controller action, tests at each
      level); how to opt a command into idempotency; how to add a business rule and
      its error code/status; clock usage (`today` passed in); testing conventions
      (one database and one clock per test; Moq only on ports).
- [ ] 7.3 `docs/ENTITIES.json`: name and format unchanged (OQ-5); **refresh the content**
      to section 6 in the same DBML-like syntax (OQ-21): all seven tables, columns,
      `[unique]`/`[not null]`, `Ref`s, the two status/timestamp check constraints as
      notes, and a header comment "illustration; the real schema is the EF configurations
      and migrations in `GrantTracker.Data`".
- 7.4 *Suggested commit point (you commit):* `docs: add architecture and back-end patterns`.

### Phase 8: Validation (the only validation phase)

- [ ] 8.1 Run section 10's agent checks in order; fix failures and re-run until all
      pass.
- [ ] 8.2 Hand over section 10's human checks.
- [ ] 8.3 Update this plan's status. *Suggested commit point (you commit), for any fixes:* `fix(backend): …`.

---

## 9. Test inventory (base)

### 9.1 `GrantTracker.Core.Tests` (unit; Moq on ports; `FakeTimeProvider`)

| Subject | Tests |
| --- | --- |
| `Mediator` | resolves the registered handler; behaviours run outermost-first in registration order; a behaviour that returns without calling `next` short-circuits (handler not called); a handler exception propagates through behaviours; a missing handler throws `InvalidOperationException` naming the request |
| `AuditBehaviour` | query → passes through (no transaction, no row); command success → `BeginTransaction`, handler, `Add(Changed)`, `SaveChanges`, `Commit` **in that order** (`MockSequence`); outcome `NoChange` set by handler is recorded; `AppException` → no commit, `DiscardChanges`, `Add(Rejected, code)`, `SaveChanges`, exception rethrown; failure while writing the rejection row → original exception still rethrown and error logged; no current user → `InvalidOperationException`; entry carries user id, action name, target, `OccurredAtUtc` from the fake clock |
| `IdempotencyBehaviour` | non-idempotent command → passes through; missing/blank/101-char key → `ValidationException(IDEMPOTENCY_KEY_REQUIRED)` and handler not called; new key → handler called once, record added with operation, hash, serialised result and `CreatedAtUtc`; same key + same command → handler **not** called, stored result returned, outcome `Replayed`; same key + different body → `IdempotencyKeyMismatchException`; same key + different command type → mismatch; race (`SaveChanges` throws `UniqueConstraintViolationException`) → `DiscardChanges`, re-read, stored result returned |
| `RequestHasher` | same command with different keys → same hash; one property changed → different hash; output is 64 lowercase hex characters |
| `BusinessClock` | UTC zone → `Today` is the UTC date; `Australia/Brisbane` (UTC+10, no DST): 13:59:59Z → day D, 14:00:00Z → day D+1; `UtcNow` has `Kind = Utc` |
| `LoginHandler` | mixed-case, padded email → finds the user (repository called with the normalised email); unknown email → `UserNotIdentifiedException(UNKNOWN_USER)`; blank email → `ValidationException` |
| `LoggingBehaviour` | `AppException` is rethrown unchanged; success returns the handler result |

### 9.2 `GrantTracker.Data.Tests` (integration; SQLite in-memory; never the real file)

| Subject | Tests |
| --- | --- |
| Migrations | all migrations apply to an empty database; `HasPendingModelChanges()` is false (the snapshot matches the model) |
| `Users` | duplicate email → `UniqueConstraintViolationException` through `EfUnitOfWork`; `FindByEmailAsync` with mixed case finds the stored user |
| `IdempotencyRecords` | two records with the same key → `UniqueConstraintViolationException`; `FindAsync` round-trips all fields |
| `Audits` | outcome stored as the text `Rejected`; FK to `Users` enforced |
| Status consistency | a task with `Status = Completed` and no `CompletedAtUtc` (and the reverse) is rejected by `CK_Tasks_Status_CompletedAt`; the same for grants with `CK_Grants_Status_ClosedAt`; consistent rows are accepted |
| Assignments | the same user assigned twice to one grant (or task) → `UniqueConstraintViolationException`; FKs to `Users`, `Grants` and `Tasks` enforced |
| UTC converter | a saved `DateTime` reads back with `Kind = Utc` and the same ticks |
| `DateOnly` | round-trips; `ORDER BY DueDate` sorts chronologically across months and years |
| `DbSeeder` | with a fake clock at 2026-03-10: 5 users, 5 grants, 6 grant assignments, 8 task assignments, expected task dates (e.g. "Verify bank details" due 2026-02-28); every task assignee is assigned to the task's grant; a second `SeedAsync` adds nothing |

### 9.3 `GrantTracker.Api.Tests` (HTTP end to end; `WebApplicationFactory`)

| Subject | Tests |
| --- | --- |
| Startup | the host starts, migrations run and the seed exists (login succeeds against the in-memory database) |
| `POST /auth/login` | `ALEX.Nguyen@Example.org ` → 200 with Alex's fixed id; unknown email → 401 `UNKNOWN_USER`; `{}` / blank email → 400 `VALIDATION_FAILED`; invalid JSON → 400 `MALFORMED_REQUEST`; works without `X-User-Id` |
| Current-user middleware (via the test-only `/__test/whoami`) | no header → 401 `USER_REQUIRED`; `not-a-guid` → 401 `UNKNOWN_USER`; unknown GUID → 401 `UNKNOWN_USER`; seeded id → 200 with that user (proves `ICurrentUser` is set) |
| Error contract | every error above has `application/problem+json`, `status`, `code` and a 32-hex `traceId`; unknown route → 404 `NOT_FOUND` ProblemDetails; `/__test/throw` → 500 `UNEXPECTED_ERROR` with no stack trace in the body |
| CORS | preflight `OPTIONS /__test/whoami` from `http://localhost:5173` requesting `X-User-Id` → 204 with the allow headers (proves CORS runs before the user check); a disallowed origin gets no `Access-Control-Allow-Origin` |

---

## 10. Validation (after all implementation)

### 10.1 Agent runs (from `backend/`)

| # | Command / check | Pass condition |
| --- | --- | --- |
| 1 | `dotnet tool restore` | `dotnet-ef` restored |
| 2 | `dotnet restore` | no errors; no package downgrade warnings |
| 3 | `dotnet build --no-restore` | 0 errors, 0 warnings |
| 4 | `dotnet ef migrations has-pending-model-changes --project src/GrantTracker.Data --startup-project src/GrantTracker.Api` | "No changes" |
| 5 | `dotnet test` | all 3 projects discovered and green; run twice to show order/date independence |
| 6 | Start the API: `dotnet run --project src/GrantTracker.Api --launch-profile http` | listening on `http://localhost:5080`; `App_Data/grant-tracker.db` created; log says migrated and seeded |
| 7 | `curl -X POST http://localhost:5080/auth/login -H "Content-Type: application/json" -d "{\"email\":\"Alex.Nguyen@example.org\"}"` | 200, id `00000000-0000-0000-0000-000000000001` |
| 8 | `curl -i -X POST http://localhost:5080/auth/login -H "Content-Type: application/json" -d "{\"email\":\"nobody@example.org\"}"` | 401 ProblemDetails, `code = UNKNOWN_USER`, 32-hex `traceId` |
| 9 | `curl -i -X POST http://localhost:5080/auth/login -H "Content-Type: application/json" -d "{\"email\":\"\"}"` | 400 `ValidationProblemDetails`, `code = VALIDATION_FAILED`, `errors.email` |
| 10 | `curl -i http://localhost:5080/nope` | 404 ProblemDetails `NOT_FOUND` |
| 11 | Inspect `logs/grant-tracker-<date>.json` | one JSON object per line; `@tr` present; the request line for step 8 has the same `@tr` as its `traceId`; the login handler's log lines carry `Request = LoginQuery` |
| 12 | Stop and start the API again | log says the seed was skipped; data unchanged |
| 13 | Grep `src/` for `DateTime.Now`, `DateTime.Today`, `ToLocalTime(` | no matches |
| 14 | Inspect `GrantTracker.Core.csproj` | no EF Core, ASP.NET Core or Serilog references |
| 15 | Stop the API (no stray processes left) | — |

### 10.2 Human checks

- Read the error bodies and the log lines and confirm they would help a support
  person (OP-4).
- Open the database (any SQLite browser) and confirm enums are readable text and
  the seed matches section 6.1.
- Review `docs/ARCHITECTURE.md` and `docs/patterns/backend.md` against the code.
- Commit the work in whatever steps you prefer (OQ-16; the suggested commit points
  are at the end of each phase).

---

## 11. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Packages at `10.0.15` on a `10.0.10` runtime | Microsoft ships patch-level packages for any `10.0.x` runtime; if restore or runtime binding complains, pin to `10.0.10` in `Directory.Packages.props` (one place) |
| xunit.v3 4.x / MTP behaviour differs from expectations (e.g. tests not discovered) | `global.json` `test.runner` is the documented fix; fallback is `xunit.v3.mtp-off` + `Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio` (VSTest), recorded if used |
| Test-host overrides not applied (eager config reads) | P10 rule; `Api.Tests` startup test fails loudly if the file database were used (the factory asserts the data source is in-memory) |
| Serilog "logger already frozen" with several test hosts | No bootstrap logger (P9) |
| Transaction misuse after rollback in `AuditBehaviour` | Rejection row is written after the transaction is disposed; covered by a Core test (call order) |
| SQLite savepoint/transaction semantics in the idempotency race path | Unit-tested with Moq now; the real two-connection race test (shared-cache) comes with the add-task feature |
| Validation only at the end, so errors accumulate | Phases are small and self-contained; Phase 8 iterates until green |
| Docs drift from code | Phase 7 reconciles after the code exists; `AGENTS.md` points the agent at the docs; updated in the same commit as later behaviour changes (C2) |

## 12. Known limitations of this plan

- No feature endpoints or BR-1 to BR-6 yet; the base is proven by the identity
  endpoints and by unit tests of the pipeline with test-only commands.
- Identification, not authentication (Q15); anyone can claim any seeded email.
- Unexpected (500) failures in commands write no audit row; the logs (with trace id)
  are the record.
- The idempotency table grows without bound (no cleanup job; TTL documented).
- Status and timestamp are two columns for one fact (OQ-3 B, OQ-4 C); check
  constraints and entity methods keep them consistent.
- Assignment tables exist and are seeded, but nothing reads or writes them until the
  assignment feature plan (OQ-20 B).
- The only production endpoint is `POST /auth/login` (OQ-11); the user check is
  proven through a test-only controller until feature endpoints exist.
- `docs/ENTITIES.json` is an illustration; the EF configurations and migrations are
  the real schema.
- Local file logs only; SQLite single writer; HTTP by default in development (HTTPS is
  an opt-in launch profile).
- No front-end work, and therefore no front-end tests (Q5).

---

## 13. Open Questions

Each question covers the detail, why it matters, its effect on this plan, the
options with pros and cons, and a recommendation. The plan body already assumes the
recommendation. Answer with the question number and an option letter (for example
"OQ-7: B"), or "approve all".

### OQ-1. New dependencies and tooling

**Detail:** `ideal.md` approved Serilog, xunit, Moq, `FakeTimeProvider` and
`WebApplicationFactory`. Three more items are needed and `AGENTS.md` requires asking
first:

- **`Microsoft.EntityFrameworkCore.Design` 10.0.15** in `Api`, with
  `PrivateAssets=all` so it is not shipped. The EF command-line tools load it from
  the startup project to generate migrations.
- **`dotnet-ef` 10.0.15** as a **local tool** (`backend/.config/dotnet-tools.json`).
  This is the command that creates migrations (`dotnet ef migrations add`). Anyone
  cloning the repo gets the same version with `dotnet tool restore`.
- **`Microsoft.Extensions.Logging.Abstractions` 10.0.15** in `Core`. It provides only
  the `ILogger` interfaces, so `LoggingBehaviour` and `AuditBehaviour` can log
  without referencing ASP.NET Core or Serilog.

**Why it matters:** Q18 decided **EF Core migrations**, which can't be generated
without the first two. Core's promise is "no framework dependency", so even an
interfaces-only package there needs an explicit yes.

**Effect:**

- Rejecting the EF items forces Q18 back to `EnsureCreated` (rejected in `ideal.md`)
  or to hand-written migrations.
- Rejecting the logging package moves `LoggingBehaviour` into `Api` (it is then no
  longer covered by `Core.Tests`) or adds a home-made logging interface to Core.

| Option | Pros | Cons |
| --- | --- | --- |
| **A. Approve all three as planned** | Standard Microsoft packages (MIT); migrations work out of the box; behaviours log through the standard `ILogger`; pinned versions | Core gains one package (interfaces only) |
| B. Approve the EF items; Core defines its own `IAppLogger` port, implemented in `Api` | Core has zero package references | Re-invents `ILogger`; more code to explain; scopes/properties must be re-plumbed by hand |
| C. Use a **global** `dotnet-ef` instead of a local tool | No manifest file in the repo | Version drift between machines; the grader must install it manually; not reproducible |
| D. Put the Design package in `Data` with a design-time `DbContext` factory | `Api` stays free of design-time packages | An extra factory class with its own design-time connection string; two places to configure the database |

**Recommendation: A.**

> **✅ Approved (2026-11-11): A, all three.**

---

### OQ-2. Assignment tables (`AssignUserTask`, `AssignUserGrant`)

**Detail:** the draft `docs/ENTITIES.json` has two link tables assigning users to
tasks and grants. The brief has no assignment feature: none of the six endpoints,
business rules or screens reads or writes them.

**Why it matters:** "Code quality & design" rewards an *appropriate* level of
abstraction, and the brief says not to over-engineer. Every table is something to
justify in the Q&A. On the other hand, the brief's background mentions officers
working in small teams, so assignment is a likely real-world next step.

**Effect:**

- Remove: the model has 5 tables (section 6); README "next steps" explains how
  assignment would be added (one link table plus a "my grants" filter).
- Keep: 2 more entities, configurations, migration tables and seed rows, with no
  code that uses them.

| Option | Pros | Cons |
| --- | --- | --- |
| **A. Remove them from the PoC; list them under next steps** | Model matches the brief exactly; nothing unused to explain; smaller migration and seed | Must explain verbally how assignment would be added |
| B. Keep the tables, unused | Schema "ready" for assignment | Dead schema; reads as over-engineering; untested |
| C. Keep and use them (e.g. a "my grants" filter) | Closer to real life | Scope creep beyond the brief; more endpoints, rules and tests |

**Recommendation: A.**

> **✅ Decided (2026-11-11): C, keep and use them.** This plan builds both tables,
> their unique pairs and FKs, and seeds assignments (section 6). *How* they are used
> (filters, endpoints, any permission rule) is a feature decision: see **OQ-20**.

---

### OQ-3. How a task records "completed"

**Detail:** BR-4 says completing a task records the date and that the date can't
change afterwards. BR-6 says a task is overdue when it is not completed and its due
date is before today. The draft has a `Status` enum and no completion date.

**Why it matters:** two fields that describe the same fact can contradict each other
(e.g. `Status = Pending` but `CompletedAtUtc` set). Every query, rule and test must
then decide which one wins. Q8 already decided that completion is a **UTC moment**.

**Effect:** with A, "completed" means `CompletedAtUtc IS NOT NULL`. The overdue query
is `CompletedAtUtc IS NULL AND DueDate < @today`. The API returns a derived
`isCompleted` (and `isOverdue`) flag rather than a stored status. "Overdue" is never
stored, because it depends on today.

| Option | Pros | Cons |
| --- | --- | --- |
| **A. `CompletedAtUtc` only (nullable)** | Single source of truth; BR-4 and BR-6 fall out naturally; nothing to keep in sync | Status is derived in queries and DTOs; adding a third state later (e.g. "Cancelled") needs a column |
| B. `Status` enum + `CompletedAtUtc` | Explicit status; easy to extend with more states | Two sources of truth; needs a check constraint or care in every write; more tests |
| C. `Status` enum only | Simplest column | Can't satisfy BR-4 (no completion date) |

**Recommendation: A.**

> **✅ Decided (2026-11-11): B, `Status` (`Pending`/`Completed`) + `CompletedAtUtc`.**
> The "two sources of truth" risk is handled by three guards (section 6): a database
> check constraint, entity methods that set both together, and `Data.Tests` proving
> the constraint. "Overdue" stays computed, never stored.

---

### OQ-4. How a grant records "closed"

**Detail:** the brief filters grants by status, and closing a grant is a one-way
transition guarded by BR-5. The draft has `Grants.Status`. OQ-3 removes the status
column from tasks, so consistency suggests asking the same question for grants.

**Why it matters:** the representation decides how the status filter, BR-1 ("no
tasks on a closed grant") and the close endpoint are written. The audit trail
already records *when* and *by whom* a grant was closed.

**Effect:** with A, `Grants.Status` is `Open`/`Closed` stored as text, indexed for
the filter; the close time lives in `Audits`. With B or C, a `ClosedAtUtc` column is
added and the filter becomes a null check.

| Option | Pros | Cons |
| --- | --- | --- |
| **A. `Status` enum (`Open`/`Closed`)** | Matches the brief's "status" filter and the draft; easy to extend if the brief's statuses grow; readable in the database | Slightly inconsistent with OQ-3 (tasks derive status, grants store it) |
| B. `ClosedAtUtc` only, status derived | Consistent with tasks; records the time on the row | The filter is expressed as a null check; extra states would need a redesign |
| C. Both `Status` and `ClosedAtUtc` | Explicit and timestamped | Two sources of truth (the problem OQ-3 avoids) |

**Recommendation: A.** The close time is already in the audit trail, so B's benefit
is small, and A reads most naturally against the brief.

> **✅ Decided (2026-11-11): C, `Status` + `ClosedAtUtc`.** Same three guards as
> OQ-3 (`CK_Grants_Status_ClosedAt`).

---

### OQ-5. `ENTITIES.json` name and format (Q21 D)

**Detail:** `docs/ENTITIES.json` contains **DBML** (`Table … { … }`, `Ref: …`), not
JSON. This was left open in `ideal.md`.

**Why it matters:** the `.json` extension misleads readers and tools. JSON viewers
reject it, and DBML editors and highlighters (dbdiagram.io, VS Code DBML extensions)
don't recognise it. Reviewers open this file to judge the data model, which the
brief asks us to explain.

**Effect:** with A, Phase 7.3 runs `git mv` and updates two links (`AGENTS.md`,
`docs/ARCHITECTURE.md`); the content is updated to section 6 either way. With B,
only the content changes. (Earlier drafts of this plan assumed B; the
recommendation changed to A once this became an explicit question.)

| Option | Pros | Cons |
| --- | --- | --- |
| **A. Rename to `docs/ENTITIES.dbml`** | Honest extension; syntax highlighting and diagram tools work; it was the recommendation in `ideal.md` | Two links to update (done in the same commit) |
| B. Keep `ENTITIES.json` | No rename | Misleading; tools can't open it |
| C. Replace it with a Markdown table inside `ARCHITECTURE.md` | One fewer file | Loses the diagram-ready format; mixes "what" docs with the model source |

**Recommendation: A.**

> **✅ Decided (2026-11-11): keep `docs/ENTITIES.json` as it is.** It is an
> **illustration of the database design**; the EF configurations and migrations are
> the real schema. `AGENTS.md` and `ARCHITECTURE.md` say so. Whether its *content*
> should be refreshed to the final model is a follow-up: **OQ-21**.

---

### OQ-6. Changes to `docs/`, `AGENTS.md` and a new `README.md`

**Detail:** `AGENTS.md` says "don't change `docs/` without developer approval". The
plan touches:

| File | Change | When |
| --- | --- | --- |
| `docs/ARCHITECTURE.md` | **Already created** at your request (marked "planned base") | Reconciled with the code in Phase 7.1 |
| `docs/patterns/backend.md` | New: how to add a use case, rule or endpoint; idempotency opt-in; audit; clock; testing conventions | Phase 7.2 |
| `docs/ENTITIES.json` | Name and format kept (OQ-5); content per OQ-21 | Phase 7.3 |
| `AGENTS.md` | Repository layout updated; a line telling the agent to read `docs/ARCHITECTURE.md` and `docs/patterns/` first | Phase 6.2 |
| `README.md` (repo root) | New; back-end run/test/reset steps, seeded users, logs, honest identity note, idempotency TTL, deploy-time migrations, Moq version note | Phase 6.1 |

**Why it matters:** Q21 decided these docs exist so you and the agent follow the
same conventions during the live change. The brief requires a README. Docs that
drift from code are worse than none, so their timing matters.

**Effect:** with A, all five land in this plan. With B, Phase 7 moves to a later
plan and the agent works from code alone during the first features. With C, only
`patterns/backend.md` is deferred.

| Option | Pros | Cons |
| --- | --- | --- |
| **A. All as planned, in Phases 6–7, after the code exists** | Docs describe real code; the patterns doc is ready before the first feature; meets Q21 C2 ("skeleton when the base is built") | More files in this change set |
| B. Defer all docs until the features are built | Less churn | The first features are built without the patterns doc; README missing for longer |
| C. Architecture + README now, patterns doc with the first feature | Patterns written from a real feature example | The first feature has no written conventions to follow |

**Recommendation: A.**

> **✅ Decided (2026-11-11): A, with your wording.**
>
> - **`README.md`** describes the project, with instructions on how to start it
>   (Phase 6.1).
> - **`AGENTS.md`** gets a documentation map: which file holds which information, and
>   when to read which (Phase 6.2).
> - `docs/ARCHITECTURE.md` and `docs/patterns/backend.md` as planned.

---

### OQ-7. Custom MVC filters

**Detail:** you listed "filters" in the base. ASP.NET Core filters run *inside* MVC
around an action (authorisation, resource, action, exception, result filters). The
cross-cutting jobs here are already placed by `ideal.md`:

- the user check is **middleware** (Q15(e));
- idempotency is a **mediator behaviour** (Q13 C);
- unhandled errors go to an **`IExceptionHandler`**, which also catches errors thrown
  by middleware (an exception filter can't);
- model validation is `[ApiController]`'s built-in filter, customised through
  `InvalidModelStateResponseFactory`.

That leaves no job that needs a custom filter. The plan has **one custom attribute**,
`[AllowAnonymousUser]`, read by the middleware.

**Why it matters:** a filter without a job is "a layer because the pattern says so",
which `ideal.md` warns against and which you'd have to justify in the Q&A. On the
other hand, if you want filters for a reason not captured here, that changes Phase 4.

**Effect:** with B, the user check moves from `CurrentUserMiddleware` to a global
`IAsyncAuthorizationFilter` (revisits Q15(e)). With C, a small action filter adds route
ids (e.g. `grantId`) to the log context. With D, an exception filter duplicates the
`IExceptionHandler`.

| Option | Pros | Cons |
| --- | --- | --- |
| **A. No custom filters; one attribute (`[AllowAnonymousUser]`)** | Nothing without a job; each concern lives where `ideal.md` put it; fewer moving parts | No "filter" to show, if that was a goal in itself |
| B. User check as a global authorisation filter instead of middleware | Idiomatic for controllers; reads the attribute naturally | Only covers MVC endpoints; reverses Q15(e); unknown routes return 404 before the user check (minor) |
| C. Add a log-context action filter that pushes route ids | Every log line has the grant/task id | `LoggingBehaviour` can add the same ids from the command; two places doing similar work |
| D. Add an exception filter | Familiar pattern | Misses middleware exceptions; duplicates `IExceptionHandler` |

**Recommendation: A.** If you want to demonstrate a filter, C is the only one with a
real (small) job.

> **✅ Decided (2026-11-11): A.**

---

### OQ-8. Controllers or minimal APIs

**Detail:** `ideal.md` didn't choose the endpoint style. The plan uses
**controllers** (`[ApiController]`, attribute routing): one `AuthController` now, and
one controller per resource later (`GrantsController`, `TasksController`).

**Why it matters:** this is the first place a reviewer or the live change looks. It
decides where attributes, validation responses and endpoint metadata come from.

**Effect:** with B, `Auth/AuthController.cs` becomes `Auth/AuthEndpoints.cs` (a route
group); the attribute is applied with `.WithMetadata(...)`; malformed JSON returns a
`BadHttpRequestException`, so the exception handler needs one more mapping instead of
`InvalidModelStateResponseFactory`.

| Option | Pros | Cons |
| --- | --- | --- |
| **A. Controllers with `[ApiController]`** | Automatic `ValidationProblemDetails` for bad bodies; attributes are natural; familiar to most reviewers; easy to find in the live change | Slightly more ceremony (classes, attributes) |
| B. Minimal APIs with route groups | Less code; fastest startup; the newer style | Validation responses need manual wiring; metadata via fluent calls; one big `Program.cs` unless split carefully |

**Recommendation: A.**

> **✅ Decided (2026-11-11): A.**

---

### OQ-9. Errors as exceptions or `Result<T>`

**Detail:** when a rule fails (e.g. BR-5), the handler must stop and the API must
return ProblemDetails. The plan throws typed exceptions (`ValidationException`,
`NotFoundException`, `ConflictException`, …, each with a stable `code`). One Api
class maps them to status codes.

**Why it matters:** this shapes every handler signature, the audit behaviour
(`Rejected` rows) and every controller action. It is a common Q&A topic
("exceptions for control flow?").

**Effect:** with B, handlers return `Result<T>`, the behaviours inspect results
instead of catching, and each controller action (or a shared helper) converts
failures into ProblemDetails. With C, rule failures are results and not-found stays an
exception.

| Option | Pros | Cons |
| --- | --- | --- |
| **A. Typed exceptions with a `code`, mapped in one place** | Least code; handlers read straight through; the audit behaviour catches one base type; one mapping table | "Exceptions for control flow" criticism; failures are not visible in method signatures |
| B. `Result<T>` everywhere | Failures explicit in signatures; no exception cost | More types and branching in every handler, behaviour and controller; a hand-written result type or a new dependency |
| C. Hybrid: results for rule failures, exceptions for the rest | Rules explicit, plumbing simple | Two error styles to explain |

**Recommendation: A.** Rejections are rare per request, so the performance argument
doesn't apply at this scale. The answer to "why exceptions?" is one mapping table and
one catch in the audit behaviour.

#### OQ-9 follow-up: does A satisfy the judges?

> "Return validation and business-rule errors with appropriate HTTP status codes and
> a consistent error body (for example, ProblemDetails)."

**Yes.** The requirement is about the **HTTP contract** (status code + body shape),
not about how the server produces it internally. Exceptions are the internal
mechanism; what leaves the API is always ProblemDetails:

| Requirement wording | How A meets it |
| --- | --- |
| **Validation errors** | `ValidationException` → **400** `ValidationProblemDetails` with a per-field `errors` map (BR-2, BR-3, missing idempotency key). Malformed JSON → the same 400 shape via `InvalidModelStateResponseFactory` |
| **Business-rule errors** | `ConflictException` → **409** (BR-1 closed grant, BR-5 pending High tasks); `NotFoundException` → **404**; idempotency misuse → **422**; unknown user → **401** (section 2.5) |
| **Appropriate status codes** | One mapping table in `AppExceptionHandler`, decided in Q16; each code has a reason a reviewer can check |
| **Consistent error body** | **Every** error, including ones the framework creates (unknown route 404, malformed body 400, unhandled 500), goes through the same `IProblemDetailsService` + `CustomizeProblemDetails`. Same fields every time: `type`, `title`, `status`, `detail`, a stable `code` (e.g. `"BR-5"`) and `traceId`; plus `errors` for 400 |
| **Proof** | `Api.Tests` assert status, `application/problem+json`, `code` and `traceId` for each error type (section 9.3); feature plans add one test per BR |

What would break it, and the guard against each:

- **An exception type nobody mapped** would surface as a 500. Guard: all expected
  errors derive from `AppException` (which requires a `code`), the mapper switches on
  that base type, and a test checks that each subclass maps to its status.
- **A controller returning its own ad-hoc error** (e.g. `BadRequest("text")`). Guard:
  the convention in `docs/patterns/backend.md` is "controllers never build error
  responses; they let exceptions flow", and every error test asserts the
  ProblemDetails shape.

The likely Q&A follow-up is "why exceptions for expected failures?". The answer:
there is one throw per rule, one catch (the audit behaviour, for the `Rejected` row)
and one mapping table. That is less code than threading `Result<T>` through every
handler, behaviour and controller, and rejections are rare enough that the cost of
throwing doesn't matter at this scale.

> **✅ Decided (2026-11-11): A**, confirmed against the requirement above.

---

### OQ-10. Mediator `Send` signature

**Detail:** a call can name both types, `mediator.Send<LoginQuery, UserDto>(query, ct)`,
or only the request, `mediator.Send(query, ct)`. The short form needs the mediator to
discover `TRequest` at runtime, as MediatR does, with a small reflection-built wrapper
cached per request type.

**Why it matters:** Q2 chose a hand-rolled mediator on condition that it stays small
and explainable line by line. Call sites appear in every controller action.

**Effect:** with B, `Mediator.cs` grows by about 20–30 lines (a wrapper class, a
cache, `MakeGenericType`) and gets one more test; call sites get shorter.

| Option | Pros | Cons |
| --- | --- | --- |
| **A. `Send<TRequest, TResponse>(request)`, both explicit** | No reflection; about 30 lines; compile-time checked; trivial to explain | Longer call sites (both type names) |
| B. `Send<TResponse>(IRequest<TResponse>)` with a cached reflection wrapper | Familiar MediatR-style call sites | Reflection and caching to explain; more code and tests |

**Recommendation: A.**

#### OQ-10 follow-up: what do the reflection and caching in B actually buy?

Reflection and caching are not benefits in themselves. **Reflection** is the price of
the shorter call; **caching** keeps that price small. The real benefits of B are:

| Benefit of B | Example | How much it matters here |
| --- | --- | --- |
| **Shorter call sites; the compiler infers the response type** | B: `await mediator.Send(new LoginQuery(email), ct);` · A: `await mediator.Send<LoginQuery, UserDto>(new LoginQuery(email), ct);` | Cosmetic. About 8 call sites in total (one per controller action) |
| **The response type is written once**, on the request (`LoginQuery : IQuery<UserDto>`) | Changing `UserDto` to `UserResponse` touches the request and handler only; with A, each call site too | Small. With A the compiler flags every call site that needs changing, so nothing breaks silently |
| **Familiar MediatR shape** | Reviewers who know MediatR recognise `Send(request)` instantly; swapping to a library later changes no call sites | Moderate for the Q&A; we rejected the libraries (Q2), so a swap is unlikely |
| **Can send a request whose concrete type isn't known at compile time** | A generic helper or test sending `IRequest<T>` values from a list; a future outbox or retry queue | None today: every controller knows its concrete command |

**What the reflection does in B:** `Send(IRequest<TResponse> request)` only knows the
*response* type at compile time. To find `IRequestHandler<LoginQuery, UserDto>` and the
matching behaviours, it calls `request.GetType()` and
`typeof(RequestHandlerWrapper<,>).MakeGenericType(...)`, and creates the wrapper with
`Activator.CreateInstance`.

**What the caching does:** that reflection is slow-ish (microseconds) and allocates.
A `ConcurrentDictionary<Type, object>` keeps one wrapper per request type, so it runs
**once per type for the app's lifetime**. After that a call costs one dictionary
lookup. The cache is purely a performance fix for B's own reflection; A needs neither.

**What B does *not* buy:** type safety and error detection are the same in both. Both
check the response type at compile time, and both find a missing handler registration
only at runtime (a DI lookup), which the mediator tests cover.

**Costs of B, restated:** about 20–30 more lines (wrapper class, cache, reflection),
one more concept to explain line by line (Q2's condition), and the reflection isn't
trimming/AOT-friendly (irrelevant for this app, but a reviewer may ask).

**Recommendation unchanged: A.** B's gains are ergonomic and amount to a few type
names at about 8 call sites, while its costs land in the one class Q2 requires you to
explain line by line. Choose B if you value MediatR-style call sites for the
presentation.

> **✅ Decided (2026-11-11): A, both type arguments explicit.**

---

### OQ-11. `GET /auth/me` and the sign-in UI (Q15(b))

**Detail:** Q15 decided `POST /auth/login` and the `X-User-Id` header, but deferred the
sign-in **UI** to planning. This plan is back-end only, so:

- the UI decision moves to the front-end plan;
- the plan adds **`GET /auth/me`**, which returns the current user for a given
  `X-User-Id`. It isn't in `ideal.md`.

**Why it matters:** the front end will keep the user id in `localStorage`. After a
reload, or after the demo database is reset, that id may be stale. `/auth/me` lets
the UI check it once at startup and show the sign-in box instead of failing on the
first real call. It also gives `Api.Tests` a simple endpoint that *requires* a user,
which proves the middleware before any feature exists.

**Effect:** with B, `AuthController` has one action, the middleware tests need a
test-only endpoint, and the front end handles stale ids from the first `401` on any
call. With C, this plan grows a front-end phase.

| Option | Pros | Cons |
| --- | --- | --- |
| **A. Add `GET /auth/me`; design the sign-in UI in the front-end plan** | Clean startup check for the UI; a real endpoint for middleware tests; the back-end plan stays back-end only | One endpoint beyond `ideal.md` |
| B. No `/auth/me`; the UI trusts its stored id | One fewer endpoint | Middleware tests need a test-only endpoint; stale ids surface as errors on the first real action |
| C. Decide the sign-in UI now (the inline "Sign in as" box recommended in Q15) | Closes Q15(b) | Mixes front-end design into a back-end plan |

**Recommendation: A.**

> **✅ Decided (2026-11-11): B, no `GET /auth/me`.** The base has one endpoint,
> `POST /auth/login`. The sign-in UI is still designed in the front-end plan, and the
> UI handles a stale stored id when the first call returns `401`. Middleware tests use
> a **test-only controller** registered by the API test factory, so no production
> endpoint exists only for tests (section 9.3).

---

### OQ-12. HTTPS and OpenAPI/Swagger in development

**Detail:** the `webapi` template adds an HTTPS profile with a dev certificate, HTTPS
redirection and an OpenAPI document. The plan removes all three: HTTP only on
`http://localhost:5080`, no OpenAPI, and a `GrantTracker.Api.http` file with
ready-made requests (VS Code / Visual Studio can run it).

**Why it matters:**

- **HTTPS:** the dev certificate must be trusted on each machine
  (`dotnet dev-certs https --trust`), a common "doesn't run on the grader's machine"
  problem. A Vite front end calling an HTTPS API with an untrusted certificate fails
  in confusing ways.
- **OpenAPI:** useful for exploring the API and for generating front-end types. It is
  also one more endpoint, which Q15(c) said must be added to the user-check
  exemptions if it is built.

**Effect:** with B, `Microsoft.AspNetCore.OpenApi` (built-in document generator) is
added, `/openapi/v1.json` is exempted from the user check, and a UI (Scalar or Swagger
UI) needs another package. With C, the HTTPS profile stays and the README adds the
certificate step.

| Option | Pros | Cons |
| --- | --- | --- |
| **A. HTTP only, no OpenAPI, a `.http` file** | Nothing to trust or install; fewest moving parts; the `.http` file doubles as runnable documentation | No browsable API docs; TLS left to deployment (README note) |
| B. Add the OpenAPI document (and optionally a UI) | Browsable docs; could generate front-end types | Extra package(s) and an exemption; more surface to test |
| C. Keep the HTTPS dev profile | Closer to production | Certificate trust step on every machine; front-end setup friction |

**Recommendation: A.** B is a good small follow-up once the feature endpoints exist.

> **✅ Decided (2026-11-11): A, HTTP only, no OpenAPI, a `.http` file.**
>
> **Amendment (2026-11-11, after implementation, at the developer's request):** an
> **opt-in `https` launch profile** was added (`https://localhost:7080`, also listening
> on `http://localhost:5080`). HTTP stays the default profile, and there is **no HTTPS
> redirection**, so the front end, the tests and existing calls are unaffected. Using
> it requires a trusted dev certificate (`dotnet dev-certs https --trust`, documented in
> the README). No OpenAPI was added.

---

### OQ-13. Test framework and runner

**Detail:** Q4 preferred xUnit v3. The current release is **xunit.v3 4.0.3** (August
2026). Version 4 **removed VSTest support on the .NET 10 SDK**, so `dotnet test` must
use **Microsoft Testing Platform (MTP)**, enabled by
`"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json`. Test projects
become small executables; `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio`
are not needed.

**Why it matters:** the brief requires "all tests with one command". That command
must work on the grader's machine and in your IDE. MTP is the direction for .NET,
but older IDEs and CI tasks may not discover MTP tests.

**Effect:** with B, the test projects reference `xunit.v3.mtp-off` plus the two VSTest
packages, and `global.json` drops the `test` section. With C, attributes and fixtures
use the v2 API, and the fixtures in Phase 5.1 change slightly.

| Option | Pros | Cons |
| --- | --- | --- |
| **A. xunit.v3 4.x on Microsoft Testing Platform** | Current, supported path on .NET 10; fewer packages; `dotnet test` unchanged for the user | Needs VS 2022 17.14+/VS 2026 or C# Dev Kit with MTP support; `global.json` must be present |
| B. xunit.v3 4.x with MTP turned off (VSTest) | Works with any IDE/CI that knows VSTest | Two extra packages; swimming against the platform direction |
| C. xunit v2 (2.9.x) | Most familiar; works everywhere | Older API; v3 was preferred in Q4; a later migration |

**Recommendation: A.** B is the documented fallback if your IDE doesn't discover the
tests (section 11).

> **✅ Decided (2026-11-11): A, xunit.v3 4.x on Microsoft Testing Platform.**

---

### OQ-14. Who owns the command transaction

**Detail:** Q14 (U2) requires two things that pull in opposite directions:

- the **success** audit row must commit **in the same transaction** as the change;
- the **rejection** audit row must be written **after** that transaction rolls back.

Q13 also requires the idempotency record in the same transaction. Something must
begin, commit and roll back that transaction.

**Why it matters:** if two components share the job, they must coordinate, and that
coordination is where atomicity bugs hide (e.g. writing the rejection row inside the
transaction that is about to roll back). This is also a likely Q&A question.

**Effect:** with B, a fourth behaviour is added, and the audit behaviour must sit
*outside* it for rejections but *inside* it for successes, which needs two audit hooks.
With C, every command handler opens and commits its own transaction.

| Option | Pros | Cons |
| --- | --- | --- |
| **A. `AuditBehaviour` owns the transaction (begin, audit, commit; or roll back, then audit the rejection)** | One component sees the whole lifecycle; both U2 rules are easy to read and test; handlers never call `SaveChanges` | The audit behaviour has two jobs (named and documented) |
| B. A separate `TransactionBehaviour` | Single responsibility per behaviour | Split audit logic (success inside, rejection outside); ordering is subtle |
| C. Handlers manage their own transaction | Explicit in each handler | Repeated code; easy to forget; audit and idempotency can't be guaranteed atomic |

**Recommendation: A.**

> **✅ Decided (2026-11-11): A, `AuditBehaviour` owns the command transaction.**

---

### OQ-15. Front end to API: CORS or a Vite proxy

**Detail:** in development the front end runs on Vite (`http://localhost:5173`) and the
API on `http://localhost:5080`. Different ports are different origins, so the browser
blocks calls unless the API allows them (**CORS**) or Vite forwards them (**dev
proxy**, so the browser sees one origin).

**Why it matters:** the custom headers `X-User-Id` and `Idempotency-Key` trigger CORS
*preflight* requests. The preflight carries no `X-User-Id`, so if CORS is wrong the
user check rejects it and every call fails with a confusing browser error.

**Effect:** with A, the API reads `Cors:AllowedOrigins`, CORS runs before the user
check, and an `Api.Tests` test covers the preflight. With B, the API has no CORS
code; the front-end plan configures the proxy, and production needs the same origin
or CORS added later.

| Option | Pros | Cons |
| --- | --- | --- |
| **A. CORS policy from configuration** | Works for any front-end setup; explicit and tested; the same mechanism in production | A config section and a middleware-order rule to remember |
| B. Vite dev proxy only | No CORS code; no preflights in development | Hides a production concern; the API can't be called from another origin |
| C. Both | Belt and braces | Two mechanisms to explain |

**Recommendation: A.**

> **✅ Decided (2026-11-11): A, CORS from configuration** ("keep it simple").

---

### OQ-16. Commit cadence with end-only validation

**Detail:** Q20 asks for frequent, unsquashed commits. You asked for validation only
after all implementation. Together, the commits after Phases 0–7 are **not build- or
test-verified** when made; Phase 8 fixes problems in follow-up commits.

**Why it matters:** reviewers read the history. Commits that don't build can look
careless, but they show the real process, which is honest.

**Effect:** with B, history has one large commit (or a few) after Phase 8. With C,
`dotnet build` runs before each commit, which partly relaxes your "validation only at
the end" instruction (no tests, build only).

| Option | Pros | Cons |
| --- | --- | --- |
| **A. One commit per phase; fixes as separate commits** | Readable, unsquashed history that matches the plan phases; follows your instruction exactly | Intermediate commits may not build |
| B. Commit only after Phase 8 | Every commit builds | Large commits; weaker evidence of process (Q20) |
| C. Per-phase commits with a quick `dotnet build` before each | Every commit compiles | A small deviation from "validation only at the end" |

**Recommendation: A**, as instructed. C is a cheap improvement if you're happy to
allow a compile-only check per phase.

> **✅ Decided (2026-11-11): the agent makes no commits and no pull requests; the
> developer commits.** Each phase ends with a **suggested commit point** (a message
> you may use), not a commit step. Validation still runs once, at the end.

---

### OQ-17. Audit rows for unexpected (500) failures

**Detail:** U2 defines four outcomes: `Changed`, `NoChange`, `Replayed`, `Rejected`
(a business-rule or validation failure). An *unexpected* error (a bug, a locked
database, a disk failure) is none of these. The plan rolls back and writes **no**
audit row; the error is logged at `Error` with the trace id.

**Why it matters:** OP-4 asks to investigate problems days later. With no row, the
database alone doesn't show that the officer tried; the logs do.

**Effect:** with B, `AuditOutcome` gains `Failed`, and the audit behaviour also
catches non-`AppException` errors and writes a `Failed` row after the rollback (same
path as `Rejected`, `ErrorCode = UNEXPECTED_ERROR`); plus one Core test.

| Option | Pros | Cons |
| --- | --- | --- |
| **A. No audit row; logs only** | Matches the four outcomes decided in U2; avoids writing to a database that may be the thing failing | The database alone can't show failed attempts |
| B. Add a `Failed` outcome | Complete attempt history in the database | A fifth outcome beyond U2; the audit write often fails for the same reason (e.g. database locked); the logs still matter |

**Recommendation: A.** It stays within the U2 decision; B is a small, contained
change later.

> **✅ Decided (2026-11-11): A.** The audit records state-changing commands (U2
> outcomes); unexpected failures go to the logs only.

---

### OQ-18. Seed data shape and fixed ids

**Detail:** section 6.1 seeds 5 fictional `example.org` users with **fixed, readable
GUIDs** (`00000000-0000-0000-0000-000000000001` …) and 5 grants whose tasks are dated
relative to the business "today" so that every rule can be demonstrated (overdue
ordering, due-today-is-not-overdue, BR-5 blocking and allowing close, a closed grant,
a grant with no tasks).

**Why it matters:** the seed is what you demo and what graders click through. Fixed
ids let the `.http` file, the README and manual tests use a known `X-User-Id`. The
scenarios decide whether each rule can be shown without first creating data.

**Effect:** with B, ids are random and must be read from `/auth/login` before every
manual call. With C, the demo needs more manual setup and fewer rules are visible
straight away.

| Option | Pros | Cons |
| --- | --- | --- |
| **A. Fixed readable ids + the scenario seed in 6.1** | Copy-paste-able ids; every rule demonstrable on first run; deterministic tests | Obviously artificial ids (fine for fictional data) |
| B. Random ids | Looks more realistic | Manual calls need a login first; docs can't show ids |
| C. Minimal seed (1–2 grants) | Less to maintain | The demo needs manual setup; edge cases not visible |

**Recommendation: A.** Tell me if you want different names, organisations or
scenarios. (Updated for OQ-2: the seed now also assigns officers to grants and tasks,
with one unassigned grant and one unassigned task; see section 6.1.)

> **✅ Decided (2026-11-11): A, fixed readable ids + the scenario seed in 6.1.**

---

### OQ-19. Compiler warning policy

**Detail:** the plan sets `WarningsAsErrors=nullable` in `Directory.Build.props`:
nullable-reference warnings fail the build, other warnings don't. Validation (section
10) still requires **0 warnings** at the end.

**Why it matters:** nullable warnings catch real null bugs. Treating *all* warnings as
errors also catches style and analyser issues (e.g. xUnit analyser hints), but can
block a build mid-change, which is risky during the 20-minute live change.

**Effect:** with B, `TreatWarningsAsErrors=true`, so any analyser or style warning
breaks the build, including during the live change. With C, nothing is enforced and
only the final validation checks warnings.

| Option | Pros | Cons |
| --- | --- | --- |
| **A. Nullable warnings as errors only; 0 warnings checked at validation** | Catches null bugs at compile time; doesn't block on cosmetic warnings mid-change | Other warnings can creep in between validations |
| B. All warnings as errors | Strictest; clean builds guaranteed | Can block the live change on a trivial warning |
| C. No warnings as errors | Never blocks | Null bugs only show up as warnings |

**Recommendation: A.**

> **✅ Decided (2026-11-11): A, nullable warnings as errors only.**

---

### OQ-20. How assignments are used (follow-up to OQ-2)

**Detail:** OQ-2 decided to **keep and use** `AssignUserGrant` and `AssignUserTask`.
This plan builds the tables, constraints and seed, but the brief defines no
assignment behaviour, so "use" needs a definition. Candidate uses:

1. **Show assignees** on grant and task responses (read only).
2. **"Assigned to me" filters**: `GET /grants?assignedTo=me` and
   `GET /tasks/overdue?assignedTo=me`.
3. **Assign/unassign commands**: e.g. `PUT /grants/{id}/assignees/{userId}`,
   `DELETE /grants/{id}/assignees/{userId}`, and the same for tasks, or an optional
   `assigneeIds` on add-task.
4. **A permission rule**: only officers assigned to a grant may change it or its
   tasks (`403`, code `NOT_ASSIGNED`).

Sub-question for 3: must a **task's assignee also be assigned to the task's grant**?
The seed already respects this. If yes, it is a Core rule checked when assigning
(`409`, code `ASSIGNEE_NOT_ON_GRANT`), not a database constraint.

**Why it matters:**

- Every use adds endpoints, rules and tests beyond the brief's six endpoints, which
  costs time and must be justified under "don't over-engineer".
- A permission rule changes the **Q16 error contract** (adds `403`). It also has a
  catch: identification is not authentication (Q15), so anyone can claim to be an
  assigned officer, which a reviewer may point out.
- Assign/unassign are state changes, so they go through the existing audit and
  idempotency pipeline. That is a good demonstration of the base, but more surface.

**Effect on this plan:** none for options A and B. The schema, constraints and seed
are the same; the choice shapes the feature plans and the front end. Option C adds
`403`/`NOT_ASSIGNED` to the error contract (section 2.5, `ARCHITECTURE.md`) and a
`ForbiddenException` to Core now.

| Option | Pros | Cons |
| --- | --- | --- |
| **A. Read-only use: show assignees + "assigned to me" filters (1, 2)** | Real, visible use of both tables; no new commands or rules; small; demo-friendly ("my overdue tasks") | Assignments only change through the seed (or a later feature) |
| B. A + assign/unassign commands with the "assignee must be on the grant" rule (1, 2, 3) | Full lifecycle; shows the audit/idempotency pipeline on more commands | 2–4 extra endpoints and a front-end UI for them; beyond the brief |
| C. B + the permission rule (1–4) | Closest to a real team workflow | Adds `403` to the contract; only advisory without real authentication; every BR test needs an assigned user; slows the live change |

**Recommendation: A**, with B's "assignee must be on the grant" rule adopted now as
the invariant for whenever assignment commands are added. C should wait for real
authentication.

> **✅ Decided (2026-11-11): B, the intended path.** Show assignees, add "assigned to
> me" filters, and add assign/unassign commands, with the rule that a task's assignee
> must be assigned to the task's grant (`409`, `ASSIGNEE_NOT_ON_GRANT`). No permission
> rule. **Built in a feature plan, not this base**: this plan delivers the tables,
> constraints and a seed that already respects the rule.

---

### OQ-21. Refresh the content of `docs/ENTITIES.json`? (follow-up to OQ-5)

**Detail:** OQ-5 keeps the file's name and format as an illustration of the database
design. Its content is still the original draft, which now differs from the decided
model:

- `Grants`: no `ReferenceNumber` or `ClosedAtUtc`; `FromOrg` instead of
  `OrganisationName`.
- `Tasks`: no `Priority`, `CompletedAtUtc` or `CreatedAtUtc`.
- `Audits`: the draft shape (`ById`, `Timestamp`, `Description`) instead of U2's.
- No `IdempotencyRecords` table.

**Why it matters:** the brief asks you to explain the data model, and reviewers (and
the agent, through the `AGENTS.md` documentation map) will open this file first. An
illustration that contradicts the code costs time in the Q&A and can mislead the
agent during the live change.

**Effect:** with A, Phase 7.3 rewrites the content in the same DBML-like syntax, and
the documentation map says "illustration, kept in sync with the schema". With B or C,
Phase 7.3 does nothing (C adds one comment line), and the map says "original draft;
the EF configurations are authoritative".

| Option | Pros | Cons |
| --- | --- | --- |
| **A. Refresh the content to section 6, same name and format** | The illustration matches the real schema; one place to explain the model from; git history still keeps the original draft | One more `docs/` edit to keep in sync on future model changes |
| B. Leave it untouched | Shows the starting point | Contradicts the code; misleading in the Q&A and for the agent |
| C. Leave it, plus a comment line at the top ("draft; see `backend/src/GrantTracker.Data/Configurations`") | Minimal change; honest | Readers still see an outdated model |

**Recommendation: A.**

> **✅ Decided (2026-11-11): A, refresh the content.** Same name and format; the file
> stays an illustration and is kept in sync with the schema (Phase 7.3).
