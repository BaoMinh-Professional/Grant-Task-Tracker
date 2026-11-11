# Grant Task Tracker

A proof-of-concept community grants portal for grants officers to manage grants and their tasks. The back end is ASP.NET Core/.NET 10, EF Core and SQLite; the planned front end uses React, TypeScript, Redux Toolkit and RTK Query. The initial API base provides demo-user identification and the cross-cutting infrastructure for later grant/task features.

## Run the API

Prerequisite: .NET SDK 10.0.302 or later.

```powershell
cd backend
dotnet tool restore
dotnet run --project src/GrantTracker.Api --launch-profile http
```

The API listens at `http://localhost:5080`; it migrates and seeds `backend/src/GrantTracker.Api/App_Data/grant-tracker.db`. Try the requests in `backend/src/GrantTracker.Api/GrantTracker.Api.http`. Demo user IDs: Alex Nguyen `00000000-0000-0000-0000-000000000001`; Priya Sharma `00000000-0000-0000-0000-000000000002`; Sam Taylor `00000000-0000-0000-0000-000000000003`; Jordan Lee `00000000-0000-0000-0000-000000000004`; Maria Garcia `00000000-0000-0000-0000-000000000005`.

### Optional: HTTPS

To run over HTTPS, trust the ASP.NET Core development certificate once per machine, then use the `https` profile:

```powershell
dotnet dev-certs https --trust
dotnet run --project src/GrantTracker.Api --launch-profile https
```

The API then listens at `https://localhost:7080` and still at `http://localhost:5080`. HTTP is not redirected, so the front end and existing calls keep working; switch `@baseUrl` in the `.http` file to use HTTPS. In production, TLS is expected to be terminated by the hosting platform or a reverse proxy.

This demo provides **identification, not authentication**. The idempotency records have no cleanup job yet; a TTL/retention cleanup job is a follow-up. In production, database migrations should run as a deployment step rather than on API startup. Moq is pinned to 4.21.0, which avoids the SponsorLink versions.

## Tests

From `backend/`, run `dotnet test`.

## Reset the demo

Stop the API and delete `backend/src/GrantTracker.Api/App_Data/grant-tracker.db` and its `-shm`/`-wal` sidecars, then restart. Logs are written to `backend/src/GrantTracker.Api/logs/` as rolling compact JSON files.
