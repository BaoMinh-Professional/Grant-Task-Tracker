# Grant Task Tracker

A proof-of-concept community grants portal for grants officers to manage grants and their tasks. The back end is ASP.NET Core/.NET 10, EF Core and SQLite; the front end uses React, TypeScript, Redux Toolkit and RTK Query. The initial API base provides demo-user identification and the cross-cutting infrastructure for later grant/task features.

## Design decisions

The data model, API design, backend architecture and their trade-offs are
documented in [ARCHITECTURE.md](docs/ARCHITECTURE.md).

## How to run

The demo database is seeded with these users. Use one of their email addresses
to sign in:

- Alex Nguyen — `alex.nguyen@example.org`
- Priya Sharma — `priya.sharma@example.org`
- Sam Taylor — `sam.taylor@example.org`
- Jordan Lee — `jordan.lee@example.org`
- Maria Garcia — `maria.garcia@example.org`

### Run the API

Prerequisite: .NET SDK 10.0.302 or later.

```powershell
cd backend
dotnet tool restore
dotnet run --project src/GrantTracker.Api --launch-profile http
```

The API listens at `http://localhost:5080`; it migrates and seeds
`backend/src/GrantTracker.Api/App_Data/grant-tracker.db`. Try the requests in
`backend/src/GrantTracker.Api/GrantTracker.Api.http`.

### Run the front end

Start the API on port 5080 first, then open a second terminal at the repository
root:

```powershell
cd frontend
npm.cmd ci
npm.cmd run dev
```

Open `http://localhost:5173` and enter one of the seeded users' email addresses
to proceed. Email sign-in is **identification, not authentication**; it does
not verify a password or protect a real account.

### Optional: HTTPS

To run over HTTPS, trust the ASP.NET Core development certificate once per machine, then use the `https` profile:

```powershell
dotnet dev-certs https --trust
dotnet run --project src/GrantTracker.Api --launch-profile https
```

The API then listens at `https://localhost:7080` and still at `http://localhost:5080`. HTTP is not redirected, so the front end and existing calls keep working; switch `@baseUrl` in the `.http` file to use HTTPS.

This demo provides **identification, not authentication**. Idempotency records are kept for 24 hours, and an hourly background job in each API copy deletes older ones (a retry after that window runs as a new request). In production, database migrations should run as a deployment step rather than on API startup. Moq is pinned to 4.21.0, which avoids the SponsorLink versions.

## Tests

From the repository root, run all frontend and backend tests with:

```powershell
.\test-all.cmd
```

For frontend linting and a production build, run `npm.cmd run lint` and
`npm.cmd run build` from `frontend/`. See [TESTING.md](TESTING.md) for the test
strategy and coverage.

## Business time

"Today" is the date in the business time zone, `Asia/Singapore`
(`BusinessTime:TimeZoneId` in `appsettings.json`). The server decides it, not the
browser, so the due-date minimum, overdue days and the BR-6 rule agree for every
officer, whatever their computer's time zone. Moments are stored and returned in
UTC.

## Assumptions about the Exercise

- Only a pending **High** task blocks Close (BR-5); Medium and Low pending tasks do not.
- Completing a task that is already complete returns `200 OK` and leaves its original
  completion time unchanged. Closing a grant that is already closed also returns `200 OK`
  without changing it.
- A closed grant is read-only: tasks cannot be added or edited, and pending tasks cannot be
  completed. Completed tasks remain unchanged. Pending tasks on closed grants are still
  included in `GET /tasks/overdue`.
- A task is overdue only if it is pending and its due date is strictly before the business
  date. A task due today is not overdue; completed tasks are never overdue.
- The exercise does not define authorization rules but require audit trails. So this demo uses seeded user with simple sign in to emulate audit.
- Status values in `?status=` are case-insensitive; any other value is a `400`.
- Editing a task changes its title, due date and priority.A completed task cannot be edited (`TASK_COMPLETED`), and a task on a closed grant is read-only (`GRANT_CLOSED`).
- Editing needs the task's version in `If-Match`. A stale version is `412 VERSION_CONFLICT`
  and a missing one is `428 PRECONDITION_REQUIRED`; the officer reloads the task and edits
  again. Retries of Add and Edit reuse the `Idempotency-Key` the browser created for that
  intent, so a retry after a lost response does not create a second task or a false conflict.
  A retry that arrives while the first request is still running waits for it and then gets the
  same result. If the server can't be reached, the form says so and keeps the key, so trying again is safe.

## Operations Needs

### Addressed in this project

#### OP-2: Concurrent officers and consistent business rules

Business rules are enforced by the API, not only by the UI. Commands run within a
database transaction, and task updates use an optimistic concurrency version:
edits send the version in `If-Match`, stale edits are rejected with
`412 VERSION_CONFLICT`, and a concurrent database update is reported as a
conflict rather than silently overwriting the task. This protects task changes
from lost updates.

This is a proof of concept, not a verified capacity claim for 100 simultaneous
officers. SQLite allows only limited concurrent writes, and the exercise-sized
solution has not been load-tested with 100 users. Before production, use a
database suited to concurrent workloads and test concurrent operations,
including adding a High-priority task while another officer closes the same
grant.

#### OP-3: Safe retries and clear connection failures

Add Task and Edit Task requests use an `Idempotency-Key`. The API stores the
successful result with the command so repeating the same request returns that
result instead of applying the change twice. Reusing the key with a different
request is rejected. The frontend keeps the key when retrying the same intent
and displays a clear message if the server cannot be reached.

Idempotency records are retained for 24 hours and cleaned up hourly. After that
retention window, a retry is treated as a new request.

#### OP-4: Support investigation after a reported problem

Commands create audit records containing the identified user, action, outcome,
error code when applicable, timestamp, trace ID and idempotency key. The API
also writes structured request and application logs to the console and to daily
rolling JSON files. Trace IDs connect an API error response with its log and
audit record.

This gives support useful context in the demo, but the files are local to each
API copy and have finite retention. A production deployment should centralize
logs and audit events, define access and retention policies, and alert on
operational failures.

### Not implemented

#### OP-1: Multiple API copies and restarts without disruption

Officers must not lose work or notice when a copy stops, crashes or restarts. The
demo cannot meet this yet: each copy uses its own local SQLite file and local log
files, so copies do not share durable state. The planned approach has these parts:

- **Stateless copies behind a load balancer.** No data is held in memory between
  requests. The load balancer sends new requests only to healthy copies (OP-5) and
  retries connection failures on another copy. It fails fast: short timeouts per attempt, and if a copy refuses the connection or times out, it tries another healthy copy, up to a small fixed number of attempts.
- **Safe retries.** Each write's business change and its idempotency record commit in
  one transaction (OP-3). If a response is lost because a copy stopped, the browser
  retries with the same `Idempotency-Key`, and whichever copy receives it returns the
  stored result instead of applying the change twice. The screen shows "reconnecting"
  while this happens.
- **Graceful restarts.** A planned stop drains in-flight requests before the copy
  exits. Deploys replace one copy at a time, and a copy receives traffic only after it
  passes readiness.
- **Unsaved input kept on the client.** The browser keeps form content until a save
  succeeds, so a refresh or restart does not lose what an officer typed. Version checks
  (`If-Match`) stop a retry from overwriting another officer's change.
- **Database failover.** A managed standby takes over if the primary fails. The EF Core
  connection retries transient errors, and the client retry above covers the gap.


#### OP-5: Health checks for each API copy

The demo does not expose health endpoints, so nothing can tell whether a copy is
serving requests or can reach its database. The planned approach is two health
routes on each API copy, with a load balancer in front that routes only to copies
reporting healthy:

- `GET /health/live` returns 200 OK while the process is running. It does not check
  the database, so a database outage does not trigger restarts.
- `GET /health/ready` returns 200 OK only when the copy can read from its database,
  within a short timeout. Otherwise it returns 503 Service Unavailable. The response
  shows status only, not connection details.

The load balancer polls `/health/ready` on each copy at a fixed interval. It removes a
copy after several consecutive failures and restores it after several consecutive
successes, so one slow response does not evict a copy. Any non-200 response counts as
unhealthy. Officers' requests then go only to healthy copies, and the idempotent
retries from OP-3 cover requests that were in flight when a copy failed.

Limits of this design:

- A load balancer only helps when all copies share one database. The demo's per-copy
  SQLite files do not meet this, so OP-1 must be addressed first.
- A load balancer stops routing but does not restart anything. Restarts need the
  hosting platform's restart policy or an orchestrator's liveness probe.
- The load balancer itself must be redundant, typically a managed service.
- Health checks do not alert people. An external uptime monitor and alert rules are
  still needed so the support team hears about outages.

#### OP-6: Operational reporting and overdue-list performance

The demo does not yet report daily activity or measure overdue-list performance.
The options below are planned, not implemented. Daily counts need a grant creation
timestamp (`CreatedAtUtc`), which the grant table does not have yet. Day boundaries
use the business time zone.

##### Daily created and completed counts

**Option A: SQL views, one for tasks and one for grants.** Each view groups its table
by business day and returns created and completed (or closed) counts. The reporting
endpoint applies the date range.

- Pros: always current, with no extra table or job; little code; counts always match
  the live data.
- Cons: recalculated on every request, so cost grows with data (needs timestamp
  indexes); views are raw SQL in a migration, which EF Core does not model natively;
  days with no activity must be filled in code; history changes if rows are edited
  or deleted.

**Option B: Statistics table filled by a nightly background job.** The job calculates
the previous business day's counts and upserts one row per day. The reporting endpoint
reads that table.

- Pros: fast reads regardless of data size; history survives later edits or deletions;
  trends are easy to chart; days with no activity are easy to store.
- Cons: today's figures appear only after the job runs (or need a separate live query);
  an extra table and job to build and test; a missed night needs catch-up logic; each
  API copy (OP-1) would run the job, so the upsert must be idempotent or a single-runner
  lock is needed.

Proposed starting point: Option A, since it is simpler at this data size. Option B
becomes worthwhile when history must be kept reliably or the data grows.

##### Overdue performance

**Scenario 1: How the application performs.** Record the duration of each
`GET /tasks/overdue` request and the number of rows returned.

- Option A: structured request log (JSON). Serilog already records elapsed time per
  request, so this needs only the row count added. Pros: no schema change, and the log
  is already written. Cons: trends need aggregation; files are local to each API copy
  and have finite retention.
- Option B: request-metrics table. Pros: queryable, easy to aggregate and chart. Cons:
  an extra write on every request, adding load to SQLite's single writer; more code to
  maintain.

Proposed starting point: Option A. Move to a table or a metrics tool if trends must be
queried centrally.

**Scenario 2: How often and for how long tasks are overdue.** Two parts:

- Record at completion: when a task is completed while overdue, write a row with the
  task ID, due date, completion time and days overdue. This is written in the same
  transaction as the Complete command.
- Nightly snapshot: a background job records the number of pending overdue tasks each
  day, so overdue volume is visible even for tasks never completed.

- Pros: the completion row captures days overdue at the moment it happened. Due dates
  are editable, so this cannot be reliably recalculated later from the task table.
- Cons: an extra write on the Complete path; the snapshot job has the same multi-copy
  and missed-night concerns as Option B above; snapshot rows grow over time (slowly,
  one per day).

### Other risks to address before production

The seeded-email sign-in is identification only, and there is no authorization:
any seeded officer can act on any grant. Production use needs real
authentication, authorization based on officer assignments, and a reviewed
backup/restore and migration process.

## What I would change before real-world use

- **Revisit the folder structure.** This project was built as a proof of concept,
  so the current organization of folders and files is not yet a settled,
  fully domain-oriented structure. I would reorganize it around the final
  feature boundaries and team conventions once those are agreed.
- **Add front-end routing.** To keep the proof of concept small, I did not add a
  routing library; the current grant and task views are navigated within the
  screen. If the application grows, I would add URL-based routes for the main
  workflows so screens can be linked, bookmarked and navigated with browser
  history.
- **Implement authentication and authorization.** Replace seeded-email
  identification with real authentication, then enforce access rules on the API
  rather than relying on the client.
- **Add pagination and server-side filtering.** The current lists are unpaged
  and filtering is client-side. I would add API query parameters for filtering,
  sorting and pagination so the application can work efficiently with larger
  data sets.
- **Introduce task types.** I expect real operations to have common categories
  of work, some of which may require particular resources or specialist teams.
  I would confirm that model with the operations team, then use task types to
  support consistent templates, resource needs and team assignment.
- **Define role- and permission-based access control.** I expect the real grant
  process may have stages where only certain roles can upload documents, change
  grant or task details, or assign work to others. I would confirm the workflow
  and permission matrix with stakeholders, then enforce those permissions in
  the API and reflect them in the UI.

## Next Steps and Time Spent

If I continued, I would work through the improvements listed in
[What I would change before real-world use](#what-i-would-change-before-real-world-use).
I estimate completing this next round of work would take one to two days.

**Time spent on the exercise:** [Add your approximate actual time spent before
submitting.]

## Known limitations

- Lists aren't pagination.
- Filters are client side.
- The `Version` column is an optimistic concurrency token. The edit action checks it, and a
  race on Complete returns `412 VERSION_CONFLICT` as well.

## Reset the demo

Stop the API and delete `backend/src/GrantTracker.Api/App_Data/grant-tracker.db` and its `-shm`/`-wal` sidecars, then restart. An existing database keeps its old seed until it is deleted, because the seed only runs on an empty database. Logs are written to `backend/src/GrantTracker.Api/logs/` as rolling compact JSON files.
