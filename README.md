# Grant Task Tracker

A proof-of-concept community grants portal for grants officers to manage grants and their tasks. The back end is ASP.NET Core/.NET 10, EF Core and SQLite; the front end uses React, TypeScript, Redux Toolkit and RTK Query. The initial API base provides demo-user identification and the cross-cutting infrastructure for later grant/task features.

## Run the API

Prerequisite: .NET SDK 10.0.302 or later.

```powershell
cd backend
dotnet tool restore
dotnet run --project src/GrantTracker.Api --launch-profile http
```

The API listens at `http://localhost:5080`; it migrates and seeds `backend/src/GrantTracker.Api/App_Data/grant-tracker.db`. Try the requests in `backend/src/GrantTracker.Api/GrantTracker.Api.http`. Demo user IDs: Alex Nguyen `00000000-0000-0000-0000-000000000001`; Priya Sharma `00000000-0000-0000-0000-000000000002`; Sam Taylor `00000000-0000-0000-0000-000000000003`; Jordan Lee `00000000-0000-0000-0000-000000000004`; Maria Garcia `00000000-0000-0000-0000-000000000005`.

## Run the front end

Start the API on port 5080 first, then open a second terminal at the repository
root:

```powershell
cd frontend
npm.cmd ci
npm.cmd run dev
```

Open `http://localhost:5173`

The email sign-in identifies a seeded demo officer; it is **identification, not
authentication**. It does not verify a password or protect a real account.

### Optional: HTTPS

To run over HTTPS, trust the ASP.NET Core development certificate once per machine, then use the `https` profile:

```powershell
dotnet dev-certs https --trust
dotnet run --project src/GrantTracker.Api --launch-profile https
```

The API then listens at `https://localhost:7080` and still at `http://localhost:5080`. HTTP is not redirected, so the front end and existing calls keep working; switch `@baseUrl` in the `.http` file to use HTTPS. In production, TLS is expected to be terminated by the hosting platform or a reverse proxy.

This demo provides **identification, not authentication**. Idempotency records are kept for 24 hours, and an hourly background job in each API copy deletes older ones (a retry after that window runs as a new request). In production, database migrations should run as a deployment step rather than on API startup. Moq is pinned to 4.21.0, which avoids the SponsorLink versions.

## Tests

From `backend/`, run `dotnet test`. From `frontend/`, run `npm.cmd run test`
(Vitest with jsdom), then `npm.cmd run lint` and `npm.cmd run build`.

## Business time

"Today" is the date in the business time zone, `Asia/Singapore`
(`BusinessTime:TimeZoneId` in `appsettings.json`). The server decides it, not the
browser, so the due-date minimum, overdue days and the BR-6 rule agree for every
officer, whatever their computer's time zone. Moments are stored and returned in
UTC.

## Assumptions

- Only a pending **High** task blocks Close (BR-5); Medium and Low pending tasks do not.
- Completing a task that is already completed returns `200` unchanged, so a retry after a
  lost response is harmless (BR-4). Closing an already closed grant also returns `200`.
- A closed grant is read-only: no tasks can be added (BR-1) and pending tasks cannot be
  completed (`GRANT_CLOSED`). Completed tasks on a closed grant stay as they are.
- Overdue means pending and due before today (BR-6). Overdue pending tasks of closed grants
  stay on `GET /tasks/overdue` (Q15).
- Status values in `?status=` are case-insensitive; any other value is a `400`.
- Editing a task changes its title, due date and priority (phase 2). The due-date rules apply
  only when the date changes, so an overdue task can still be renamed. A completed task cannot
  be edited (`TASK_COMPLETED`), and a task on a closed grant is read-only (`GRANT_CLOSED`).
- Editing needs the task's version in `If-Match`. A stale version is `412 VERSION_CONFLICT`
  and a missing one is `428 PRECONDITION_REQUIRED`; the officer reloads the task and edits
  again. Retries of Add and Edit reuse the `Idempotency-Key` the browser created for that
  intent, so a retry after a lost response does not create a second task or a false conflict.
  A retry that arrives while the first request is still running waits for it and then gets the
  same result. If the server can't be reached, the form says so and keeps the key, so trying again is safe.

## Known limitations

- Lists are unpaged (OQ-8).
- The grants list, the `canClose` flag and "today" can be stale between refetches. The screen
  refetches when a tasks view or the Add task dialog opens and on window focus, and does not
  poll (OQ-5). The server still enforces every rule, so a stale screen shows the refusal.
- Pending Medium and Low tasks on a closed grant cannot be completed.
- No authorisation: any seeded officer can act on any grant.
- The `Version` column is an optimistic concurrency token. The edit action checks it, and a
  race on Complete returns `412 VERSION_CONFLICT` as well.

## Reset the demo

Stop the API and delete `backend/src/GrantTracker.Api/App_Data/grant-tracker.db` and its `-shm`/`-wal` sidecars, then restart. An existing database keeps its old seed until it is deleted, because the seed only runs on an empty database. Logs are written to `backend/src/GrantTracker.Api/logs/` as rolling compact JSON files.

