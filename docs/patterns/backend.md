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
