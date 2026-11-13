# How Run tests

Prerequisites: .NET SDK 10 and Node.js/npm. The all-at-once script installs
frontend dependencies from the lock file before running tests, so it requires
an internet connection and recreates `frontend\node_modules` each time.

## Run all at once

From the repository root, either click `test-all.cmd` in File Explorer or run:

```cmd
.\test-all.cmd
```

The script continues to the backend tests if frontend dependency installation
or tests fail. At the end it summarizes whether the frontend and backend suites
passed, failed, or were skipped; individual failure details are shown in the
test output above. It returns a non-zero exit code if any step fails and waits
for a key press before closing so you can review the output. The backend
solution includes the Core, Data, and API test projects. `dotnet test` restores
backend NuGet dependencies as needed. This command runs tests only; it does not
run lint or production builds.

## Run one by one

From the `frontend` directory, install dependencies the first time (or after
the lock file changes), then run the frontend tests:

```cmd
cd frontend
npm.cmd ci
npm.cmd run test
```

Run all backend tests from the `backend` directory:

```cmd
cd backend
dotnet test GrantTracker.slnx
```

You can also open `backend\GrantTracker.slnx` in Visual Studio, right-click the
solution, and choose **Run Tests**.

## Testing strategy

## What is tested and why

- **Frontend (Vitest with jsdom):** focused tests cover formatting and filtering,
  API error conversion, RTK Query requests, and the grants/task screens. Form
  tests exercise client validation, API rule errors, retry/idempotency behavior,
  and the server-unreachable message. These tests give quick feedback on UI
  behavior without requiring a browser or a running API.
- **Core (xUnit):** domain and handler tests cover business rules and use cases
  independently of HTTP and persistence. Fixed test dates and fake business
  clocks cover due-date boundaries, overdue calculations, completion
  idempotence, and grant closure rules.
- **Data (xUnit + SQLite):** integration tests use SQLite to exercise EF
  migrations, constraints, repository queries, and persisted idempotency
  records against the database provider the application uses.
- **API (xUnit + `WebApplicationFactory`):** HTTP-level tests run through the
  test server with an isolated SQLite database and a controllable business
  clock. They verify endpoint behavior, status codes and error bodies, including
  the BR-1–BR-6 rules across the API boundary.

This split keeps business-rule failures easy to diagnose while still checking
that persistence and HTTP contracts work together. The frontend and API are
tested at their respective boundaries; they are not currently exercised
together in a deployed-browser end-to-end test.

## Reliability and risk coverage

Date-sensitive backend tests use fixed dates or a fake business clock rather
than the machine's current date. Database-backed tests create isolated test
databases, and the frontend test configuration restores mocks between tests.
The suites should therefore be repeatable and independent of test execution
order.

One operations risk covered is **OP-3 (requests may be repeated over unreliable
connections)**. API tests verify that replaying an Add Task request with the
same idempotency key returns the original task instead of creating a duplicate,
and that reusing a key with a different body is rejected. Frontend tests verify
that an unreachable-server error is shown and a retry keeps the original key.

## Deliberate gaps and remaining risk

The test suite does not simulate production load, multiple API instances
sharing a production database, abrupt process restarts, or real regional network
failures. It also does not currently run a browser-level end-to-end test against
a deployed frontend and API. Those gaps leave uncertainty around capacity,
deployment behavior, and issues that only appear across the full system. Before
launch, add load and multi-instance tests using the production database setup,
deployment/health-check verification, and a small end-to-end browser smoke
test. Authentication and authorization also need a separate production
security review; these tests do not establish that the demo identity approach
is suitable for real accounts.

## Checking AI-assisted tests

When AI helps write a test, review that it asserts the externally meaningful
behavior and would fail if that behavior regressed. As a mutation check, the
overdue boundary was deliberately changed so that a task due today counted as
overdue; the focused Core test for a task due today failed. The original
condition was restored, and the Core suite passed again. This check is a useful
example, not a substitute for reviewing each test's setup and assertions.
