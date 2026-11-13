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

The demo uses a local SQLite database file and local log files, so multiple API
copies would not share durable state or logs. For production, move persistence
to a shared managed database, use centralized logging, and deploy stateless API
copies behind a load balancer. Add restart and failover tests to verify that
committed work remains available.

#### OP-5: Health checks for each API copy

The demo does not expose a health endpoint that verifies database connectivity.
Add separate liveness and readiness checks, with readiness confirming that the
copy can reach its data store, then connect them to the hosting platform's
traffic-routing and restart policies.

#### OP-6: Operational reporting and overdue-list performance

The demo does not provide daily task-created/task-completed reporting or
performance dashboards. Before production, add reporting or telemetry for those
counts and measure overdue-list query duration as data grows. Use the results to
guide indexing and pagination work; the current lists are unpaged.

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
