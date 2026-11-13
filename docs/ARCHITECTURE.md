# Architecture

> **Status:** matches code. Update it **in the same commit** as any behaviour change
> (`AGENTS.md`).
> **Decision log:** [.github/workflows/setup-repo/ideal.md](../.github/workflows/setup-repo/ideal.md)
> (why), [.github/workflows/setup-repo/setup-be-plan.md](../.github/workflows/setup-repo/setup-be-plan.md)
> (how). No ADRs and no diagrams, by decision (Q21).

## 1. Purpose

Grant Task Tracker is a small proof of concept for grants officers: see grants, add
and complete tasks, and spot overdue work. The back end is an ASP.NET Core Web API
on .NET 10 with EF Core and SQLite. The React, TypeScript, Redux Toolkit and RTK
Query front end follows the [UI style](UI-STYLE.md) and
[front-end patterns](patterns/frontend.md).

Guiding principles:

- **Keep it simple:** three source projects, no layer without a job.
- **Business rules in plain C#:** no framework types, "today" passed in, unit-tested
  directly.
- **Safe retries (OP-3) and investigable history (OP-4)** are built into the request
  pipeline, not into individual endpoints.
- **Common, free libraries** over bespoke code, except the ~30-line mediator (see 5.1).

## 2. Solution layout

```text
backend/
  .config/dotnet-tools.json  local dotnet-ef tool manifest
  GrantTracker.slnx
  global.json                 SDK pin; `dotnet test` uses Microsoft Testing Platform
  Directory.Build.props       net10.0, nullable (warnings as errors), implicit usings
  Directory.Packages.props    every package version, in one place
  src/
    GrantTracker.Api/         HTTP: controllers, middleware, ProblemDetails, configuration, DI wiring
    GrantTracker.Core/        entities, business rules, use cases, mediator and behaviours, ports
    GrantTracker.Data/        EF Core + SQLite: DbContext, configurations, migrations, repositories, seed
  tests/
    GrantTracker.Core.Tests/  unit
    GrantTracker.Data.Tests/  integration with SQLite in-memory
    GrantTracker.Api.Tests/   HTTP end to end with WebApplicationFactory
```

## 3. Layers and dependency direction

| Project | Responsibility | References | Must not reference |
| --- | --- | --- | --- |
| **Core** | Entities, enums, business rules, use cases (one folder each: command/query + handler), mediator, pipeline behaviours, error types, business clock, **ports** (`IUnitOfWork`, `IUserRepository`, `IAuditLog`, `IIdempotencyStore`, `ICurrentUser`) | nothing but `Microsoft.Extensions.Logging.Abstractions` | EF Core, ASP.NET Core, Serilog |
| **Data** | Implements Core's ports with EF Core + SQLite; schema (configurations, migrations); seed; database initialiser; translates SQLite unique violations into a Core exception | Core | Api, ASP.NET Core |
| **Api** | HTTP surface: controllers, request DTOs, current-user middleware, ProblemDetails mapping, Serilog, CORS, options, **all DI wiring** | Core, Data | — |

Core sits at the centre and owns the interfaces; Data depends on Core (not the
reverse), so business logic can be tested without a database.

## 4. Request flow

### 4.1 HTTP pipeline (Api), in order

1. **Exception handler:** maps exceptions to ProblemDetails (section 6).
2. **Status-code pages:** gives empty error responses (e.g. an unknown route's 404)
   a ProblemDetails body.
3. **Serilog request logging:** one summary line per request (method, path, status,
   elapsed time, user id).
4. **Routing.**
5. **CORS:** answers preflights before any user check.
6. **Current-user middleware:** resolves `X-User-Id` to a seeded user; `401` if it's
   missing or unknown. Skipped for endpoints marked `[AllowAnonymousUser]` (only
   `POST /auth/login`).
7. **Controller:** maps the DTO to a command or query and calls
   `IMediator.Send<TRequest, TResponse>`. No business logic.

### 4.2 Mediator pipeline (Core), outermost first

| Order | Behaviour | Applies to | Job |
| --- | --- | --- | --- |
| 1 | `LoggingBehaviour` | all requests | Log scope with request name, user id and idempotency key; duration; rejections at Warning |
| 2 | `AuditBehaviour` | commands (`ICommand`) | **Owns the transaction.** Success: writes the audit row and commits once. `AppException`: rolls back, then writes a `Rejected` audit row in a separate transaction and rethrows |
| 3 | `IdempotencyBehaviour` | commands marked `IIdempotentCommand` | Requires the key; replays a stored result for a repeated key and the same request hash; `422` on a mismatch; stores a new result in the same transaction; resolves concurrent retries through the unique key |
| — | Handler | the one use case | Loads through ports, applies rules with `today` passed in, changes entities, reports the audit target/outcome via `IAuditContext`. Never calls `SaveChanges` |

Queries pass through behaviours 2 and 3 untouched; they are not audited and run
without an explicit transaction.

### 4.3 HTTP API design

The API uses resource routes for grants and tasks, with explicit action routes
for business transitions:

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/auth/login` | Identify a seeded officer by email |
| `GET` | `/grants?status=Open` | List grants, optionally filtered by status, with task counts |
| `GET` | `/grants/{id}` | Get a grant with its tasks |
| `POST` | `/grants/{id}/tasks` | Create a task on a grant |
| `PUT` | `/grants/{id}/close` | Close a grant |
| `GET` | `/tasks/overdue` | List overdue tasks across grants |
| `PUT` | `/tasks/{id}` | Edit a task |
| `PUT` | `/tasks/{id}/complete` | Complete a task |

Creating a task under its grant makes the relationship explicit and lets the
server enforce grant-specific rules. Task editing, completion and overdue
listing use task routes because those operations act on tasks independently of
the grant detail view. `close` and `complete` are explicit action routes rather
than generic status updates, so the server can apply the corresponding business
rules as one operation.

Successful task creation returns `201 Created`. Task responses include an ETag
derived from the task version; edits require that version in `If-Match` to
reject stale updates instead of overwriting newer changes. Add and Edit accept
an `Idempotency-Key` so a retry after a lost response can return the original
result. Completing an already completed task is a no-op, preserving its original
completion time. The common ProblemDetails error shape and status codes are
defined in section 6.

The grant and overdue lists are currently unpaged. This keeps the proof of
concept simple, but means pagination and corresponding API parameters should be
added before these lists are used with a large data set.

## 5. Patterns in use

### 5.1 Hand-rolled mediator

`Send` plus ordered pipeline behaviours, nothing else: no notifications, no
streaming, no assembly scanning. Handlers and behaviours are registered explicitly
in `Api/DependencyInjection`. Behaviour order = registration order. 
It existsbecause its pipeline carries real cross-cutting work (audit, idempotency, logging)
 inside the business transaction. MediatR was rejected for its commercial licence
### 5.2 CQRS-lite, one folder per
 use case

Each use case is one folder in Core (e.g. `Users/Login/`) holding its command or
query and its handler. Commands implement `ICommand<T>` (audited, transactional);
queries implement `IQuery<T>`. All use the same `DbContext`.

### 5.3 Ports and adapters

Core declares the interfaces it needs; Data implements them. Handlers depend only
on Core interfaces, so they are unit-tested with Moq. Repositories are tested for
real against SQLite in-memory.

The repository ports are focused on the queries and changes the use cases need:
grants, tasks and users each have their own interface. Data implements these with
EF Core queries rather than a generic CRUD repository. Read-only grant lists use
no-tracking projections with task counts; detail and task operations load the
related entities they need. This keeps EF Core out of Core while avoiding a
second, generic abstraction over `DbContext`. The trade-off is a small amount of
interface and mapping code in exchange for independently testable handlers and
SQLite-backed repository tests.

`GrantTrackerDbContext` is registered as scoped by `AddDbContext`. Repositories,
the audit and idempotency stores, and `EfUnitOfWork` use that same scoped context,
so tracked entity changes and related records participate in the command's
transaction. Repositories do not save changes themselves: the Unit of Work
provides the save, commit and rollback boundary described in the mediator
pipeline above. Read-only queries use `AsNoTracking` where appropriate. The
context applies entity configurations from the Data assembly and central
conversions for UTC timestamps and `DateOnly` values.

### 5.4 Errors as typed exceptions

Core throws `AppException` subclasses that carry a stable `code`. The Api maps them
in one place (section 6). Request DTOs have nullable properties and no data
annotations; validation is a Core concern.

## 6. Error contract

Every error is `application/problem+json` with `status`, `title`, a stable `code`
and `traceId` (the 32-hex W3C trace id, identical to the log field `@tr`).

| Status | When | `code` |
| --- | --- | --- |
| 400 | invalid input (`ValidationProblemDetails` with `errors`) | `VALIDATION_FAILED`, `MALFORMED_REQUEST`, `IDEMPOTENCY_KEY_REQUIRED`, `BR-2`, `BR-3` |
| 401 | missing or unknown user, unknown login email | `USER_REQUIRED`, `UNKNOWN_USER` |
| 404 | unknown id or route | `NOT_FOUND` |
| 409 | valid input, conflicting state | `BR-1` (task on a closed grant), `BR-5` (close with a pending High task), `GRANT_CLOSED` (complete or edit a pending task on a closed grant), `TASK_COMPLETED` (edit a completed task) |
| 412 | edit with an `If-Match` version that is not the task's current version | `VERSION_CONFLICT` |
| 422 | idempotency key reused with a different request | `IDEMPOTENCY_KEY_MISMATCH` |
| 428 | edit without an `If-Match` header | `PRECONDITION_REQUIRED` |
| 500 | unexpected failure (generic message, no stack trace) | `UNEXPECTED_ERROR` |

Business rules are checked in this order for **Add task** (Q7): the grant exists
(404), then the input (`BR-3`, `BR-2`, required fields, all errors in one `400`),
then the closed-grant rule (`BR-1`).

## 7. Cross-cutting concerns

| Concern | Design |
| --- | --- |
| **Identity** | Seeded `Users`; `POST /auth/login { email }` (case-insensitive) returns the user; the client sends `X-User-Id` on every other request; stateless (works across API copies). **Identification for audit context, not authentication.** |
| **Time** | `TimeProvider` is the only source of "now". `IBusinessClock` gives `UtcNow` and `Today` (the date in the configured business timezone, IANA id, default `UTC`). Rules and queries take `DateOnly today` as an argument. Moments are stored in UTC and read back as UTC; calendar dates are `DateOnly`. No `DateTime.Now`, `DateTime.Today` or `ToLocalTime()`. |
| **Idempotency (OP-3)** | Opt-in per command (`IIdempotentCommand`); the controller copies the `Idempotency-Key` header into the command. Stored: key (PK), operation, SHA-256 of the command without the key, serialised result, `CreatedAtUtc`. Successes only. TTL documented; no cleanup job yet. `PUT`s are idempotent by checking the current state. |
| **Audit (OP-4)** | One `Audits` row per command: user, action, target, outcome (`Changed`/`NoChange`/`Replayed`/`Rejected`), error code, trace id, idempotency key, UTC time. |
| **Logging and correlation (OP-4)** | Serilog: console plus a daily rolling compact-JSON file (`logs/grant-tracker-<date>.json`). Every line carries the trace id (`@tr`); user id, request name and idempotency key are added as properties. The same trace id is in ProblemDetails and audit rows. |
| **Persistence** | SQLite file `App_Data/grant-tracker.db` (relative to the content root, git-ignored). EF Core migrations are applied at startup; seed data (relative to "today") is added only to an empty database. Enums are stored as text. |
| **Configuration** | `appsettings.json` sections `ConnectionStrings`, `BusinessTime`, `Cors`, `LogFile`, `Serilog`. Read lazily through options so the test host can override them. No secrets. |
| **CORS** | Allowed origins from configuration (default the Vite dev server); allows `X-User-Id`, `Idempotency-Key`, `Content-Type`, `If-Match`; exposes the `ETag` response header so the browser can read a task's version. |

## 8. Data model

The real schema is defined by the EF Core configurations and migrations in
`GrantTracker.Data`. [ENTITIES.json](./ENTITIES.json) is an **illustration** of the
database design (DBML syntax), kept in sync with that schema.

| Table | Purpose | Notes |
| --- | --- | --- |
| `Users` | Seeded officers (identification) | Email unique, stored lowercase |
| `Grants` | Grant records | Unique `ReferenceNumber`; `Status` (`Open`/`Closed`) + `ClosedAtUtc`, kept consistent by a check constraint |
| `Tasks` (entity `GrantTask`) | Tasks on a grant | `Status` (`Pending`/`Completed`) + `CompletedAtUtc`, kept consistent by a check constraint; `Title` at most 100 characters (`CK_Tasks_Title_Length`); `Version` is the optimistic concurrency token (starts at 1, increases on every change); "overdue" is computed from `DueDate` and today, never stored |
| `AssignUserGrant` | Officers assigned to a grant | Present but not used by the grant and task features (Q16); unique `(GrantId, UserId)` |
| `AssignUserTask` | Officers assigned to a task | Present but not used by the grant and task features (Q16); unique `(TaskId, UserId)` |
| `Audits` | One row per command (U2) | User, action, target, outcome, error code, trace id |
| `IdempotencyRecords` | Stored results of idempotent commands | Keyed by the idempotency key |

Guid keys everywhere except `IdempotencyRecords`. Enums are stored as text.

## 9. Testing

| Project | Level | Database | Clock | Doubles |
| --- | --- | --- | --- | --- |
| `Core.Tests` | unit: rules, handlers, mediator, behaviours, clock | none | `today` argument / `FakeTimeProvider` | Moq on Core ports only |
| `Data.Tests` | integration: queries, constraints, migrations, seed | SQLite in-memory, fresh per test | `FakeTimeProvider` | none |
| `Api.Tests` | HTTP end to end: status codes, ProblemDetails, middleware order | named SQLite in-memory per test host | `FakeBusinessClock` set on `GrantTrackerApiFactory.Clock` (fixed day, settable) | none |

One command from `backend/`: `dotnet test`. Tests never open the real database file
and never depend on the real date or on test order. Front-end tests run with
`npm.cmd run test` from `frontend/` (Vitest with jsdom; `mockApi` replaces `fetch`).

## 10. Known limitations

- No authentication or authorisation (identification only).
- SQLite has a single writer; local file logs; neither suits several API copies
  (OP-1/OP-2 are out of scope for now).
- The idempotency table grows without bound until a cleanup job exists.
- Unexpected (500) failures write no audit row; the logs are the record.
- Lists have no pagination: `GET /grants` returns every grant and `GET /grants/{id}` every task.
- Filters are on client side.
- Data can be stale between refetches (OQ-5): the grant detail refetches when the tasks view or the Add task dialog opens and on window focus, with no polling. The server still enforces every rule, so a stale screen shows the refusal.
