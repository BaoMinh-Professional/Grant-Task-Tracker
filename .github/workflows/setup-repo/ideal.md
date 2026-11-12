# Setup repo — Idea & Abstract

> **Stage:** 1 — Idea & abstract (decision record). **Status:** ✅ **Green**,
> approved by the developer on 2026-11-11.
> **No implementation plan here.** The next stage is `plan.md` (same folder).
>
> **Carried into planning:**
>
> - Q7 (data model, deferred);
> - Q15(b) (sign-in UI, deferred);
> - Q21 D (renaming `docs/ENTITIES.json` to `.dbml`, unanswered when this was
>   approved).

---

## 1. Context

### 1.1 The exercise (source: `Software-Developer-Technical-Exercise.pdf`)

Build a small **Grant Task Tracker**: grants officers see grants, add tasks, complete
tasks and spot overdue tasks.

- **Stack required by the brief:** C# / ASP.NET Core Web API, EF Core (SQLite or
  in-memory), React + TypeScript.
- **Back end:** 6 endpoints (list grants with pending-task count and status filter,
  get grant with tasks, add task, complete task, close grant, list overdue tasks
  most-overdue first). Business rules **BR-1 to BR-6**. Consistent error body
  (e.g. ProblemDetails). Seed data relative to the app start date.
- **Operations needs (section 3):** build **at least two** of OP-1 to OP-6, and explain
  the rest in the README.
- **Testing:** BR-1 to BR-6 covered, tests at **more than one level**, at least one
  test for an OP risk, **one command**, independent of the real date and of test
  order. One-page testing strategy.
- **Front end:** one screen: grant picker, task list with overdue highlighted, Add
  task form (client validation + API errors), Complete button.
- **Submission:** full commit history (no squash), README, the AI agent setup
  committed, `AI_USAGE.md`.
- **Assessment signals that shape this document:** "Keep it simple; don't
  over-engineer", "appropriate level of abstraction", "you must be able to explain
  every line", and a **20-minute live change** in the presentation.
  - Weights: testing 20%, AI use 20%, correctness 15%, code quality 15%, OP needs 10%,
    front end 10%, communication 10%.

### 1.2 Current repository state (traced 2026-11-11)

- **Git:** `master` branch has **no commits yet**. Everything is untracked.
- **`AGENTS.md`:** stack (.NET 10, React + TS + RTK / RTK Query, SQLite) and working
  agreement. Two rules matter here: **"don't change `docs/` without developer
  approval"** and **"ask before adding dependencies or tooling"**.
- **`docs/ENTITIES.json`:** draft data model in DBML-like syntax. Tables: `Users`,
  `Grants`, `Tasks`, `Audits`, `AssignUserTask`, `AssignUserGrant`. Gaps are
  listed in Q7.
- **`backend/`:** folders `src/GrantTracker.Api`, `src/GrantTracker.Core`,
  `src/GrantTracker.Data`, `tests/GrantTracker.Api.Tests`,
  `tests/GrantTracker.Core.Tests`. They contain **only `obj/`**: no `.csproj`, no
  solution and no source files. This is effectively a clean slate, but the three-layer
  split plus two test levels is already implied by the folder names.
- **`frontend/`:** does not exist yet.

---

## 2. The ideal

Build a **small, correct, well-tested** base that:

1. Separates **API**, **business logic** and **data access** in a way that is easy to
   justify in a short Q&A, with no layer that exists "because the pattern says so".
2. Keeps business rules in **plain, framework-free code** with an injectable clock,
   so BR-1 to BR-6 are tested directly and deterministically.
3. Makes **retries safe** and **failures visible** (OP-3), and makes **past problems
   investigable** by people who didn't build it (OP-4).
4. Can be extended quickly and safely during the **live change** (e.g. "add BR-7"):
   one obvious place to add a rule, one obvious place to add a test.
5. Uses **common, widely used, commercially free** libraries over bespoke solutions,
   unless bespoke code is very small and clearly justified.

---

## 3. Divergence axes

| Axis                  | Option A                                                              | Option B                                                     | Option C                                                              | Trade-off                                                                                                                                                           |
| --------------------- | --------------------------------------------------------------------- | ------------------------------------------------------------ | --------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Solution layering     | Full Clean Architecture (Domain / Application / Infrastructure / Api) | **3 projects: Api / Core / Data** (already in repo)          | Single project, folders only                                          | More layers mean more ceremony and more to defend; one project blurs the "separate API / logic / data" ask                                                          |
| Use-case dispatch     | MediatR                                                               | Source-generated mediator (`martinothamar/Mediator`, MIT)    | **Hand-rolled mediator** (user preference) / direct handler injection | Library = less code but a dependency and possible licence concerns; hand-rolled = full control but more lines to defend; direct injection = least code, no pipeline |
| CQRS depth            | Separate read/write models or stores                                  | **CQRS-lite: one handler per command/query, same DbContext** | No CQRS (service classes)                                             | Full CQRS is unjustifiable at 6 endpoints; service classes grow into "god services"                                                                                 |
| Test doubles          | Moq                                                                   | NSubstitute                                                  | **Real collaborators + `FakeTimeProvider` + SQLite in-memory**        | Mocks verify interactions, not behaviour; mocking `DbContext` hides query bugs                                                                                      |
| FE data layer         | Plain `fetch` + `useState`                                            | TanStack Query                                               | **Redux Toolkit + RTK Query**                                         | RTK Query gives caching, retry, `refetchOnReconnect` and typed error states; slightly heavier for one screen                                                        |
| OP needs built        | OP-1 / OP-2 (scale, concurrency)                                      | **OP-3 + OP-4**                                              | OP-5 / OP-6 (health, metrics)                                         | OP-3/4 address the most likely day-1 field problems and are testable; the others are out of this scope (Q19)                                                        |
| Idempotency mechanism | Client-only dedupe                                                    | **Server-side `Idempotency-Key` persisted in DB**            | Natural idempotency only                                              | Client dedupe can't survive lost responses; natural idempotency doesn't cover `POST`                                                                                |

---

## 4. Decisions (approved 2026-11-11)

### 4.1 Committed

- **Back end:** .NET 10 (LTS), ASP.NET Core Web API, EF Core 10 with **SQLite**
  (mandatory).
- **Front end:** React + TypeScript on **Vite** (no meta-framework; PoC scale), with
  **Redux Toolkit + RTK Query** for state and API calls (Q6). The pattern is recorded
  in `docs/` as a reusable boilerplate.
- **Back-end tests (Q4, Q5):** **xUnit** across three projects, all run by one
  `dotnet test`:
  - `Core.Tests`: business rules, services and command/query handlers, using Moq for
    repository seams.
  - `Data.Tests`: repositories on **SQLite in-memory**; the real database is never
    touched.
  - `Api.Tests`: middlewares and the HTTP contract, through `WebApplicationFactory`.

  **No front-end tests** for now (time constraint).

- **Clock:** inject .NET's built-in **`TimeProvider`** wherever "today" matters
  (BR-2, BR-6, seeding). Tests use **`FakeTimeProvider`**. Business logic never calls
  `DateTime.Now`.
- **Dates and time (Q8):**
  - Moments in time are stored in **UTC**.
  - Calendar dates (due date, application date) are **`DateOnly`**.
  - "Today" is `TimeProvider` converted to one configured **business timezone**
    (default `"UTC"`) and passed into rules and queries.
- **Operations needs (Q19):** build **only OP-3 and OP-4** in this scope. OP-1,
  OP-2, OP-5 and OP-6 are out of scope for now and handled later (including the
  README explanation the brief asks for).
- **OP-3, server side:** non-idempotent requests carry an **idempotency key
  (RequestId)** that is **persisted in the database**, so a repeated request returns
  the original result instead of acting twice.
- **OP-3, client side:** RTK Query error states distinguish **"server unreachable /
  timed out"** (`FETCH_ERROR` / `TIMEOUT_ERROR`) from **business-rule errors**
  (ProblemDetails). Unreachable is shown clearly on screen, never silently.
- **Error contract (Q16):** ProblemDetails for every error, with a stable `code`
  (e.g. `"BR-1"`) and `traceId`.
  - `400` request input (BR-2, BR-3, missing idempotency key).
  - `401` unknown or missing user.
  - `404` unknown id.
  - `409` state conflict (BR-1, BR-5).
  - `422` idempotency-key misuse.
- **Front end on an unreliable network (Q17):**
  - About 10 seconds request timeout.
  - A persistent "can't reach the server" banner.
  - Automatic retry for queries; a manual "Try again" for mutations.
  - The idempotency key is created once per user intent and reused on retry.
- **Database lifecycle (Q18):** EF Core migrations at startup; seed only if the
  database is empty; the README covers resetting the demo database.
- **AI-setup artefacts (Q20):** `AGENTS.md`, `prompt-style.md` and the
  per-feature ideal/plan files are committed so the workflow and decisions are
  visible. History is unsquashed.
- **Repeated `PUT`s (Q9, Q10):**
  - Completing an already-completed task returns `200` with the task; the original
    completion time is unchanged.
  - Closing an already-closed grant returns `200` as a no-op. BR-5 is checked only
    on the open → closed transition.
- **Idempotency scope (Q11):** one **reusable, opt-in** idempotency mechanism.
  - Only `POST /grants/{id}/tasks` opts in today, and the key is **required** there.
  - `PUT`s stay naturally idempotent by checking the current state first. If the
    target state already holds, they change nothing and never record a second
    "changed" audit entry (Q14.2).
- **Idempotency mechanism (Q12, Q13):** a **mediator pipeline behaviour** that a
  command opts into with a marker interface.
  - The endpoint copies the `Idempotency-Key` header into the command.
  - Stored: key, operation, request hash, the serialised handler result and
    `CreatedAtUtc`, all in the same transaction as the change.
  - A unique index on the key handles concurrent retries.
  - Replay → the stored result; a hash mismatch → `422`; only successes are stored.
  - No cleanup job yet; the TTL is documented.
- **Audit trail (Q14 E):** **mandatory** for state changes and user actions.
  - **U2:** one row per command, with an outcome. Written by an audit pipeline
    behaviour and stamped with the traceId and the user.
- **Logging and correlation (Q14 A, D):**
  - Serilog writes to a rolling JSON file; the trace id is on every line.
  - ProblemDetails carries `traceId`, shown in the UI as an 8-character reference.
- **Identity (Q15):** a seeded `Users` table (`Id`, `Name`, `Email`; 5 fictional
  `example.org` users) with email-only sign-in.
  - This is **identification for audit context, not authentication**, and the
    README says so.
  - Stateless `X-User-Id` header resolved by middleware; unknown or missing → `401`.
  - **Sign-in is `POST /auth/login`**, the only endpoint that doesn't require a
    user. The sign-in UI is decided in planning.
- **Workflow:** idea, then approval, then plan, then approval, then code
  (`prompt-style.md`). These docs are part of the AI-setup deliverable.
- **Solution layering (Q1 → B):** three source projects, `GrantTracker.Api` (HTTP,
  ProblemDetails, DI wiring), `GrantTracker.Core` (entities, business rules, use cases;
  no EF Core / ASP.NET dependency) and `GrantTracker.Data` (EF Core + SQLite). Keep it
  simple; no further layers.
- **Use-case dispatch (Q2 → C):** a **hand-rolled minimal mediator** in `Core`,
  scoped to **send + pipeline behaviours** only (no notifications, streaming or
  assembly-scanning magic). It earns its place by carrying the cross-cutting OP-3/OP-4
  concerns in its pipeline (see Q13, Q14), and it has its own tests.
- **CQRS depth (Q3 → B):** CQRS-lite. One class per command/query handler, all on the
  same `DbContext`. Each use case lives in **one folder** holding both its
  command/query contract and its handler, so a use case never spreads across the
  workspace.
- **Repository documentation:** the repo's **architecture and patterns** are recorded
  as docs under **`docs/`**, alongside the data model (`docs/ENTITIES.json`). They
  describe what the code _is_ (layers, dependency direction, request flow, patterns
  in use and how to extend them), as distinct from `.github/<feature>/` ideal/plan
  files, which describe how a feature was _decided_. Per `prompt-style.md`, these docs
  are written once the base is green and kept current when behaviour changes
  (`AGENTS.md`).
  - **Shape (Q21):** `docs/ARCHITECTURE.md` plus **separate back-end and front-end
    pattern docs**. No diagrams.
  - **Timing:** a skeleton when the base is built, then updated in the same commit
    as each behaviour change.

### 4.2 Rejected (and why)

- **MediatR:** moved to a commercial licence from v13. It fails the
  "commercially free" criterion and adds a licensing question to the Q&A.
- **Next.js / other meta-frameworks:** routing, SSR and server components are
  unnecessary for one screen. The brief explicitly allows Vite.
- **Mocking `DbContext` / repositories over EF:** hides LINQ-to-SQL translation
  issues, such as SQLite's limits on ordering by `DateTimeOffset`. A real SQLite
  database is cheap and catches real bugs.
- **Full CQRS (separate read store):** no scaling or read/write asymmetry justifies
  it at this size.
- **Client-only duplicate suppression for OP-3:** a lost _response_ still makes the
  user retry, so only the server can guarantee "once".
- **Full Clean Architecture (4+ projects) (Q1):** too much ceremony for 6 endpoints;
  the 3-project split already answers "separate API, logic and data".
- **`martinothamar/Mediator` (Q2):** common and free, but a source generator is harder
  to explain line by line than a ~40-line dispatcher, and it adds a dependency.
- **No mediator / direct handler injection (Q2):** would have been the least code, but
  cross-cutting OP-3/OP-4 logic would then move into ASP.NET filters, outside the
  use-case transaction.

### 4.3 Under discussion

Q1–Q6 and Q8–Q21 are decided (see 4.1 and the ✅ notes in section 6). **Deferred
to planning:** Q7 (data model) and the Q15 sign-in UI. **Still open:** Q21 D only
(renaming `docs/ENTITIES.json` to `.dbml`).

---

## 5. Known limitations

- **No authentication or authorisation** is in the brief. Q15's proposed email-only
  sign-in gives the audit a "who", but it is **identification, not
  authentication**: anyone can claim any seeded email. Real authentication is the
  first "before going live" item.
- **SQLite allows a single writer.** That's fine for a PoC and conveniently
  serialises writes. It's not representative of OP-1/OP-2 (multiple API copies,
  ~100 concurrent officers). The README must say what would change: a server
  database, optimistic concurrency, a shared idempotency store.
- **The idempotency store grows without bound** unless expired keys are cleaned up.
  The PoC **won't build cleanup** (Q12, point 6). The README documents the intended
  TTL and the cleanup approach.
- **Local-file logs** (OP-4) don't survive container rebuilds or scale across API
  copies. Production needs a central log store.
- **No commits yet.** The brief values visible commit history, so the base should be
  committed in small, meaningful steps from the start.
- **No front-end tests (Q5).** The front end's validation, ProblemDetails display and
  the "server unreachable" banner (OP-3 client half) are verified manually only. The
  risk is that a regression in the UI's error handling goes unnoticed; the API tests
  cover the server side of the same rules. Vitest + React Testing Library is the first
  addition if time allows.

---

## 6. Open Questions

Each question covers why it matters, the options with pros and cons, and a
**recommendation for the current context** (2-hour-ish PoC, graded on judgement and
explainability, with a live change).

### Q1. How deep should the architecture go: Clean Architecture or the existing 3 projects?

**Why it matters:** "Code quality & design" (15%) explicitly rewards an
_appropriate_ level of abstraction, and the brief says "don't over-engineer". Every
extra project or abstraction is something to defend in Q&A and to navigate during
the 20-minute live change.

| Option                                                                    | Pros                                                                                                                      | Cons                                                                                                                        |
| ------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| A. Full Clean Architecture (Domain, Application, Infrastructure, Api)     | Textbook dependency rule; familiar to reviewers                                                                           | 4+ projects and interfaces for 6 endpoints; high ceremony; slower live change                                               |
| **B. 3 projects: `Api` / `Core` / `Data`** (matches the existing folders) | Maps 1:1 to the brief's "API, business logic, data access"; still keeps the domain free of EF/ASP.NET; already scaffolded | The Core-to-Data boundary needs a small decision (does Core define the DbContext abstraction, or does Core reference Data?) |
| C. Single project with folders                                            | Fastest; fewest files                                                                                                     | Weakest answer to "separate the layers in a way you can justify"; nothing enforces boundaries at compile time               |

**Recommendation: B.** It is "Clean Architecture-lite": `Core` holds entities,
rules and use-case handlers with no framework dependencies; `Data` holds EF Core and
SQLite; `Api` holds HTTP, ProblemDetails mapping and DI wiring. The open sub-decision
(the direction of the Core/Data reference) belongs in `plan.md`.

> **✅ Decided (2026-11-11): B.** "Let's keep this simple." The Core/Data reference
> direction is now tied to Q4: if repositories are tested on their own, `Core`
> defines repository interfaces and `Data` implements them (Data → Core), which keeps
> `Core` free of EF Core.

---

### Q2. Should we use a mediator, and if so, a library or our own?

**Why it matters:** this is the most visible design choice in the back end. A
hand-rolled mediator is code you must explain line by line. A mediator with no
pipeline behaviours adds indirection without benefit ("why not just call the
handler?" is a likely Q&A question). It also decides where OP-3/OP-4 cross-cutting
logic can live (see Q13).

| Option                                                  | Pros                                                                                                                        | Cons                                                                                                                                                          |
| ------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A. MediatR                                              | Industry-familiar                                                                                                           | Commercial licence from v13 (rejected in 4.2)                                                                                                                 |
| B. `martinothamar/Mediator` (source-generated, MIT)     | Common, free, fast, has pipeline behaviours; satisfies "prefer a common lib"                                                | An extra dependency (needs approval per `AGENTS.md`); source-generator magic is harder to explain than plain code                                             |
| C. **Hand-rolled minimal mediator** (user preference)   | Full control; small (~30–50 lines); demonstrates understanding; pipeline behaviours for logging/idempotency are easy to add | Reinvents a solved problem (counter to "name the category, pick the common lib"); must be tested itself; risk of scope creep (notifications, streaming, etc.) |
| D. No mediator: inject handlers directly into endpoints | Least code; zero indirection; easiest to explain and live-change                                                            | Cross-cutting concerns go into ASP.NET endpoint filters/middleware instead of a use-case pipeline                                                             |

**Recommendation: D, with C as an acceptable alternative under a strict condition.**
Inject one handler per use case directly (CQRS-lite, see Q3). Choose C only if its
**pipeline actually carries OP-3/OP-4 concerns** (e.g. a logging/correlation
behaviour, or a transactional idempotency behaviour). Then keep it to "send +
behaviours" only, and give it its own tests. If chosen, record the "why not
`martinothamar/Mediator`" answer: an explicit ~40-line dispatcher is easier to explain
than a source generator, and it avoids an extra dependency.

> **✅ Decided (2026-11-11): C, hand-rolled mediator.** Guardrails that must hold so
> it stays defensible:
>
> - **Scope:** `Send` + ordered pipeline behaviours only. No notifications/events,
>   streaming or reflection-based assembly scanning. Handlers are registered
>   explicitly in DI.
> - **Justification:** the pipeline must carry real cross-cutting work, as a minimum
>   request logging/correlation (OP-4) and the transactional idempotency check
>   (OP-3, Q13). If it ends up with no behaviours, revisit.
> - **Tests:** the dispatcher itself is covered (correct handler resolved, behaviour
>   order, short-circuit on failure).

---

### Q3. How much CQRS?

**Why it matters:** CQRS shapes the folder structure and how a new rule is added in
the live change.

| Option                                                                                            | Pros                                                                                                                                                     | Cons                                                          |
| ------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------- |
| A. Full CQRS (separate read models/stores, projections)                                           | Scales reads independently                                                                                                                               | No justification at this size; much more code                 |
| **B. CQRS-lite: one class per command/query, same `DbContext`; queries project straight to DTOs** | Small, focused, testable units; reads can use `AsNoTracking` + projection (good for "pending count" and "overdue" queries); obvious place for a new rule | Slightly more files than a service class                      |
| C. Service classes (`GrantService`, `TaskService`)                                                | Fewest files                                                                                                                                             | Tends to grow into large classes; rules spread across methods |

**Recommendation: B.**

> **✅ Decided (2026-11-11): B, one class per command/query handler, with
> co-location.** Each use case gets one folder (vertical slice) containing **both** its
> contract (the command/query type, e.g. `AddTaskCommand` implementing the mediator's
> request interface) **and** its handler, to avoid the workspace expanding into
> parallel `Commands/`, `Queries/` and `Handlers/` trees. Illustrative shape only;
> exact names belong in `plan.md`:
>
> ```text
> GrantTracker.Core/
>   Grants/
>     CloseGrant/        CloseGrantCommand + CloseGrantHandler
>     GetGrant/          GetGrantQuery + GetGrantHandler
>     ListGrants/        ListGrantsQuery + ListGrantsHandler
>   Tasks/
>     AddTask/           AddTaskCommand + AddTaskHandler
>     CompleteTask/      CompleteTaskCommand + CompleteTaskHandler
>     ListOverdueTasks/  ListOverdueTasksQuery + ListOverdueTasksHandler
> ```
>
> _Assumption to confirm:_ "interface" here means the command/query contract. A
> separate per-handler interface (e.g. `IAddTaskHandler`) is **not** needed, because
> the mediator resolves handlers through its generic handler interface.

---

### Q4. Testing data access without touching the real DB, and testing time ✅ _Decided_

**Your requirements (2026-11-11):**

1. Test the **repositories** too, not only business rules and HTTP.
2. Tests must **never change the real database** (the app's SQLite file).
3. Time-dependent rules (BR-2, BR-6, plus the seed) must be tested through a
   **controllable clock**.
4. (From earlier) xUnit + Moq is the preferred toolset.

**Why it matters:**

- **Testing (20%):** tests must be meaningful, reliable, independent of the real
  date and of test order (brief §4.1).
- **Repositories hold real logic.** Several rules are decided in the _query_, not in
  C#:
  - the pending-task count per grant;
  - the status filter;
  - "overdue" (not completed and due before today) with **most-overdue-first**
    ordering;
  - the **unique index** that makes OP-3 idempotency safe under concurrent retries.

  A test that mocks the repository can't catch a wrong `WHERE`/`ORDER BY`, an EF query
  that SQLite can't translate, or a missing unique index.

- **Q1 link:** repository tests imply `Core` defines repository interfaces and `Data`
  implements them.

#### 4a. How do repository tests get a database that is real but disposable?

| Option                                                                                 | Pros                                                                                                                                                                                                                                           | Cons                                                                                                                                                                                                                                                          |
| -------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A1. EF Core **InMemory** provider                                                      | One line of setup; very fast                                                                                                                                                                                                                   | **Not a relational database**: no SQL translation, no unique constraints, no real transactions. LINQ runs in memory, so queries SQLite can't run still pass. The OP-3 unique-index test would be meaningless. Microsoft's own docs discourage it for testing. |
| **A2. SQLite in-memory (`DataSource=:memory:`), one open connection per test**         | **The same engine as production**: real SQL translation, constraints, unique indexes, transactions. Nothing touches disk; the database disappears when the connection closes. Fresh per test, so order doesn't matter. Milliseconds to create. | The connection must stay open for the test's lifetime (one small fixture). A multi-connection race test needs a named shared-cache variant (`Data Source=test-{guid};Mode=Memory;Cache=Shared`).                                                              |
| A3. A temp SQLite **file** per test (unique name, deleted afterwards)                  | Real engine; also allows multiple connections easily                                                                                                                                                                                           | Disk I/O; cleanup can fail and leave files (especially on Windows file locks)                                                                                                                                                                                 |
| A4. The real dev database, with each test wrapped in a transaction that is rolled back | No schema setup                                                                                                                                                                                                                                | **Violates "don't change the real DB"** in practice: code that commits its own transaction (the idempotency write) breaks the rollback; state leaks between tests; file locks; results depend on whatever data is in the file                                 |

**Recommendation: A2.** Each repository test gets a brand-new in-memory SQLite
database with the same schema as production, built with the same EF model (ideally
by applying the migrations, see Q18, so migrations are tested too). The app's real
database file is never opened by tests. Use the shared-cache variant only for the one
concurrency test that needs two connections.

**What to test at repository level:** only the logic that lives in queries and
constraints (pending counts, status filter, overdue filter and ordering, `DateOnly`
round-trip and ordering, the idempotency-key uniqueness). Trivial add/get-by-id
mapping isn't worth a dedicated test; it is covered by the API tests.

#### 4b. How do we control "today"?

.NET 8+ ships the abstract **`TimeProvider`** in the base library, so the production
code needs no extra package.

| Option                                                                                      | Pros                                                                                                               | Cons                                                                                    |
| ------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------- |
| **B1. `FakeTimeProvider`** (Microsoft, package `Microsoft.Extensions.TimeProvider.Testing`) | The standard, purpose-built test clock: set any instant, advance time, set the local timezone. Common, free (MIT). | A test-only package (needs approval per `AGENTS.md`)                                    |
| B2. Hand-rolled `FixedTimeProvider : TimeProvider` (override `GetUtcNow`)                   | Zero dependency; ~5 lines                                                                                          | Reinvents B1; timezone and timer support must be hand-written if needed                 |
| B3. Moq a `TimeProvider`                                                                    | Uses the tool already chosen                                                                                       | Setup noise for something a fake does better; easy to forget timezone (`LocalTimeZone`) |
| B4. Custom `IClock` interface                                                               | Familiar older pattern                                                                                             | Non-standard since .NET 8; duplicates `TimeProvider`                                    |

Design point that makes this much simpler: **pass "today" in rather than reading it
inside rules or queries.**

- **Pure business rules** (BR-2, BR-6) take `DateOnly today` as a plain argument, so
  Core unit tests need **no clock at all**: `IsOverdue(task, today: 2026-03-10)`.
- **Repository queries** take `today` as a parameter (e.g. the overdue query). Never
  use SQLite's `date('now')` inside SQL: that would be the real clock hidden in the
  database and impossible to control in tests.
- **Only the edge** (handlers/mediator, seeding) asks `TimeProvider` for "now" and
  converts it to the business-timezone `DateOnly` (Q8). That is where the fake clock
  is injected: handler tests and API tests (by replacing the `TimeProvider`
  registration in the test host).
- Edge cases to cover with the fake clock: due date == today (not overdue), due
  yesterday (overdue), just before and after midnight in the business timezone (Q8).

**Recommendation: B1 (`FakeTimeProvider`) + the "pass today in" design.** If the
extra package is not approved, B2 is an acceptable 5-line fallback.

#### 4c. Where does Moq fit?

Moq stays in the toolset, used where it is the best tool rather than everywhere:

| Use Moq for                                                                                                                                                                    | Don't use Moq for                                          |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------- |
| **Handler tests in `Core.Tests`**, mocking the repository interfaces to verify orchestration, e.g. "BR-1 fails → `Add` is never called", "BR-5 fails → the grant is not saved" | `DbContext` / `DbSet` (use A2 instead)                     |
| **Failure simulation** that is hard to produce for real, e.g. the repository throws a unique-constraint violation, to test the idempotency race path and its error mapping     | `TimeProvider` (use B1)                                    |
| **Mediator pipeline tests** where verifying call order or short-circuiting is the point                                                                                        | Anything an API or repository test already covers for real |

Use **Moq ≥ 4.20.2** (the release after SponsorLink was removed), and note the
version choice in the README.

#### 4d. Resulting test levels (affects Q5)

| Project                             | Level                                                                      | Database                                                 | Clock                                  | Moq                |
| ----------------------------------- | -------------------------------------------------------------------------- | -------------------------------------------------------- | -------------------------------------- | ------------------ |
| `GrantTracker.Core.Tests` (exists)  | Unit: rules (BR-1 to BR-6), handlers, mediator pipeline                    | none                                                     | `today` passed in / `FakeTimeProvider` | yes (repositories) |
| **`GrantTracker.Data.Tests` (new)** | Integration: repositories and constraints                                  | SQLite in-memory                                         | `today` passed as a parameter          | no                 |
| `GrantTracker.Api.Tests` (exists)   | End-to-end HTTP: status codes, ProblemDetails, OP-3 duplicate-request test | SQLite in-memory (replaces the file DB in the test host) | `FakeTimeProvider`                     | no                 |

Sub-decision: a new **`Data.Tests`** project versus putting repository tests in
`Api.Tests`.

- A separate project mirrors the src layout 1:1 and keeps each test project's purpose
  obvious.
- Merging saves one project but mixes two levels.

**Recommendation: a separate `Data.Tests` project.** All three still run with one
`dotnet test` at the solution root.

**Overall recommendation for Q4:** xUnit + Moq (scoped as in 4c) + SQLite in-memory
per test (4a, A2) + `FakeTimeProvider` with the "pass today in" design (4b) + a new
`Data.Tests` project (4d). The xUnit version (v3 preferred, otherwise v2) is
confirmed during planning.

**Dependencies needing approval (`AGENTS.md`), test projects only:** `Moq`,
`Microsoft.Extensions.TimeProvider.Testing` and `Microsoft.AspNetCore.Mvc.Testing`
(for `WebApplicationFactory`). The SQLite in-memory driver comes with EF Core's
SQLite provider, so it needs nothing extra.

> **✅ Decided (2026-11-11): all recommendations accepted.**
>
> - **4a → A2:** SQLite in-memory, a fresh database per test; the real database file
>   is never opened by tests. The shared-cache variant is used only for a
>   multi-connection race test.
> - **4b → B1 + "pass today in":** `FakeTimeProvider`. Rules and queries take
>   `DateOnly today` as an argument; only handlers, seeding and the API read
>   `TimeProvider`. No `date('now')` in SQL.
> - **4c → Moq, scoped:** repository interfaces in handler tests, failure simulation,
>   pipeline order. Never on `DbContext` or `TimeProvider`.
> - **4d → three back-end test projects:** `Core.Tests`, a new `Data.Tests`, and
>   `Api.Tests`.
> - **Packages approved for test projects** (implied by these choices): `Moq`
>   (≥ 4.20.2), `Microsoft.Extensions.TimeProvider.Testing`,
>   `Microsoft.AspNetCore.Mvc.Testing`. The exact versions and the xUnit version
>   are pinned in `plan.md`.

---

### Q5. Which test levels, and how do we keep "one command"?

**Why it matters:** the brief requires tests at **more than one level**, **all run
with one command**, independent of date and order. Adding front-end tests (Vitest)
introduces a second runner.

| Option                                                                                                                          | Pros                                                                 | Cons                                                                                          |
| ------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| **A. Back end only: `Core.Tests` (rules, unit) + `Api.Tests` (HTTP + SQLite, integration); `dotnet test` at the solution root** | Already two levels; one native command; matches the existing folders | Front end untested; must be stated honestly in the strategy                                   |
| B. A + Vitest for the front end, with a root script (`npm test` or a PowerShell/bash script) that runs both                     | Covers FE validation and error display                               | Two toolchains; "one command" becomes a wrapper script that must work on the grader's machine |
| C. A + Playwright end-to-end                                                                                                    | Strongest confidence end to end                                      | Heavy setup; flaky-risk; out of proportion for ~2 hours                                       |

**Recommendation: A for the base, with B as a later increment if time allows.** The
OP-3 risk test (same idempotency key twice creates one task) lives in `Api.Tests`.
Each test builds its own database and clock, so there is no shared state and order
doesn't matter.

> **Note (2026-11-11):** if Q4 is accepted as recommended, option A gains a third
> back-end level, `Data.Tests` (repositories on SQLite in-memory). It is still one
> `dotnet test` command (see the Q4 4d table).

> **✅ Decided (2026-11-11): A, back end only; no front-end tests for now** (time
> constraint).
>
> **What gets tested, and where:**
>
> | Area                                                                                                                                                                  | Project      | How                                                                                                                                                                                               |
> | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
> | **Middlewares** (if any, e.g. exception → ProblemDetails, correlation/traceId)                                                                                        | `Api.Tests`  | Through the in-process test host (`WebApplicationFactory`), so the real pipeline order is exercised. A direct unit test with `DefaultHttpContext` only if a middleware has non-trivial branching. |
> | **Business rules** (BR-1 to BR-6)                                                                                                                                     | `Core.Tests` | Pure unit tests; `today` passed in; no database, no mocks                                                                                                                                         |
> | **Services** (supporting services, e.g. the "today in business timezone" helper, the mediator dispatcher and its pipeline behaviours such as idempotency and logging) | `Core.Tests` | Unit tests; `FakeTimeProvider`; Moq for repository seams and failure simulation                                                                                                                   |
> | **Commands/Queries** (handlers)                                                                                                                                       | `Core.Tests` | Unit tests with mocked repository interfaces (orchestration: the rule is checked, then saved or not saved)                                                                                        |
> | **Repositories**                                                                                                                                                      | `Data.Tests` | SQLite in-memory; query logic and constraints only                                                                                                                                                |
> | **Endpoints / HTTP contract** (status codes, ProblemDetails, the OP-3 duplicate-request risk test)                                                                    | `Api.Tests`  | End-to-end through the test host, with SQLite in-memory and `FakeTimeProvider`                                                                                                                    |
>
> - **One command:** `dotnet test` at the solution root runs all three projects.
> - **Assumption:** "Services" means supporting services as listed above. Use cases
>   are handlers, not service classes (Q3), so there is no `GrantService` /
>   `TaskService` to test.
> - **Endpoints row:** added even though it wasn't in your list, because the brief
>   requires status codes, ProblemDetails and an OP risk test, and that's where
>   middlewares get exercised anyway.
> - **Front end:** untested; recorded as a known limitation (section 5) and in the
>   testing strategy, with Vitest as the first item on "what we'd add next".

---

### Q6. Front-end state: RTK + RTK Query, or something lighter?

**Why it matters:** `AGENTS.md` already lists RTK / RTK Query. The question is
whether it is proportionate for one screen, and how it supports OP-3.

| Option                           | Pros                                                                                                                                              | Cons                                                                               |
| -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| A. Plain `fetch` + React state   | Smallest                                                                                                                                          | Hand-written loading/error/retry/refetch; easy to "fail silently"                  |
| B. TanStack Query                | Lighter than Redux; excellent async state                                                                                                         | Diverges from `AGENTS.md`; no global store (none needed though)                    |
| **C. Redux Toolkit + RTK Query** | Typed endpoints; cache invalidation after add/complete; `retry`, `refetchOnReconnect`, `FETCH_ERROR`/`TIMEOUT_ERROR` states directly support OP-3 | Store boilerplate for one screen; likely **no custom slices** beyond the API slice |

**Recommendation: C**, used mainly as RTK Query. Don't add Redux slices without a
concrete need, and state that in the README.

> **✅ Decided (2026-11-11): C, Redux Toolkit + RTK Query.** The front-end pattern
> must be **recorded in `docs/` as a reusable boilerplate** once it exists. That means
> how to declare an API slice and endpoint, the tags used for cache invalidation, how
> a mutation passes the idempotency key, how ProblemDetails and unreachable errors
> are mapped for display, and when (not) to add a Redux slice. It belongs in the
> front-end section of the patterns doc (see Q21).

---

### Q7. Data model: what to change in `docs/ENTITIES.json`? ⏸ _Deferred to planning_

> **⏸ Deferred (2026-11-11):** the data model will be discussed in detail during
> planning (`plan.md`). The gaps and options below are kept as **input for that
> discussion only**; nothing here is decided, and `docs/ENTITIES.json` stays
> unchanged until then.

**Why it matters:** the brief requires designing and explaining the data model. The
current draft **misses fields required by the brief** and **adds tables outside its
scope**. Per `AGENTS.md`, `docs/` changes need developer approval, so this must be
decided explicitly.

Gaps against the brief:

| Entity                                       | Missing / questionable                                          | Required by             |
| -------------------------------------------- | --------------------------------------------------------------- | ----------------------- |
| `Grants`                                     | `ReferenceNumber` (unique, e.g. `GR-100001`)                    | Background, seed data   |
| `Grants`                                     | `ApplicationDate`                                               | BR-2, seed data         |
| `Grants`                                     | `FromOrg` naming → e.g. `OrganisationName`                      | Clarity                 |
| `Tasks`                                      | `Priority` (Low/Medium/High)                                    | BR-5, seed data         |
| `Tasks`                                      | Completion date (`CompletedOn`)                                 | BR-4                    |
| `Tasks`                                      | `Status` enum _and_ a completion date can contradict each other | BR-4 / BR-6 consistency |
| —                                            | An idempotency-key table                                        | OP-3 (see Q12)          |
| `Users`, `AssignUserTask`, `AssignUserGrant` | Not required; no authentication in the brief                    | — (scope)               |
| `Audits.ById`                                | Non-null FK to `Users`, but there is no authenticated user      | OP-4 (see Q15)          |

Options for the out-of-scope tables:

| Option                                                                 | Pros                                                     | Cons                                                                                   |
| ---------------------------------------------------------------------- | -------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| A. Keep users and assignments                                          | Closer to the real domain ("small team per grant", OP-2) | Unused code and tables; reads as over-engineering; nothing in the brief exercises them |
| **B. Remove them from the PoC model and list them under "next steps"** | Model matches the brief exactly; easier to explain       | Must explain how assignment/ownership would be added later                             |

Options for task status:

| Option                                                                                                            | Pros                                                     | Cons                                   |
| ----------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------- | -------------------------------------- |
| A. `Status` enum + `CompletedOn`                                                                                  | Explicit                                                 | Two sources of truth that can disagree |
| **B. `CompletedOn` nullable only; "completed" = `CompletedOn != null`; "overdue" computed from due date + today** | Single source of truth; BR-4 and BR-6 fall out naturally | Status must be derived in queries/DTOs |

**Recommendation:** add the missing fields; adopt **B** for both tables (remove
users/assignments; derive status from `CompletedOn`); add an idempotency table; and
keep `Audits` without the user FK (see Q15). Use **Guid** primary keys (as drafted)
plus a **unique `ReferenceNumber`** for humans.

---

### Q8. Date types and the meaning of "today"

**Why it matters:** BR-2 ("due date not in the past, not before the application
date") and BR-6 ("overdue when due date is before today") are date-only rules. Using
`DateTime` brings time-of-day and timezone bugs. Officers are in **several regional
offices**, so "today" can differ between offices around midnight. On top of that,
**EF Core's SQLite provider can't translate ordering or comparison on
`DateTimeOffset`**, which would break "most overdue first".

| Option                                                                                                                        | Pros                                                                                        | Cons                                                                  |
| ----------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| A. `DateTime` everywhere                                                                                                      | Familiar                                                                                    | Time-of-day noise; UTC vs local ambiguity; easy off-by-one on "today" |
| **B. `DateOnly` for `ApplicationDate` and `DueDate`; "today" = `TimeProvider` converted to one configured business timezone** | Exact match to the rules; SQLite stores it as sortable `yyyy-MM-dd` text, so ordering works | Requires choosing that timezone (an assumption to document)           |
| C. `DateOnly` + per-office timezone                                                                                           | Most correct for multi-office                                                               | Needs an office concept on users/grants; out of scope                 |

**Recommendation: B.** Add a configurable business timezone (default documented in
the README as an assumption). For `CompletedOn` (BR-4: "records the date"), use a
`DateOnly` from the same clock. If OP-4 needs exact times, the audit/log timestamp
provides them in UTC.

#### Q8 follow-up ✅ _Decided_: "today = TimeProvider, and store datetimes in UTC"?

**Your proposal (2026-11-11):** `TimeProvider` is the source of "now"/"today", and
datetimes are saved in UTC across the system and the database.

**What is clearly right about it:**

- **`TimeProvider` as the only source of "now"** is already the design (Q4 4b), so
  this is agreed.
- **UTC for every moment in time** (when something _happened_) is the industry
  standard. It applies to: completed-at, created-at, audit timestamps, log
  timestamps and idempotency-key creation times.
  - It makes the stored value independent of the server's timezone, which matters
    for **OP-1**: several API copies may run on machines with different local
    timezones.
  - It makes OP-4 investigations unambiguous ("what happened at 03:12Z").
- **Rule that follows:** no `DateTime.Now`, `DateTime.Today` or `ToLocalTime()`
  anywhere on the server. The server's own timezone must never affect behaviour.

**Where "UTC everywhere" breaks: calendar dates and "today".**
`DueDate` and `ApplicationDate` aren't moments; they are **calendar days** ("due on
10 March"). "Overdue", "in the past" and "before the application date" are
comparisons between **days**, so the question is _whose_ day "today" is.

1. **Storing a calendar date as a UTC datetime shifts it.** An officer in UTC+10
   picks "10 March". If the browser turns that into local midnight and then into
   UTC, it's stored as `2026-03-09T14:00Z`, which reads back as **9 March**: an
   off-by-one-day bug. Many bugs of this kind come from exactly this conversion.
2. **"Today" as the UTC date is wrong for part of every day** for officers who
   aren't in UTC.
   - Example, office in UTC+10 (e.g. eastern Australia): between 00:00 and 10:00 local
     time, the UTC date is still _yesterday_. A task due yesterday (local) **isn't
     shown as overdue until 10am**, and BR-2 **accepts a due date that is already in
     the past** locally.
   - In a UTC−x office the error goes the other way in the evening: tasks turn
     overdue before the local day ends.
   - The window is the timezone offset: 7 hours in UTC+7, 10–11 hours in eastern
     Australia.
3. **SQLite/EF detail:** EF's SQLite provider stores `DateTime` as sortable text, but
   **reads it back as `DateTimeKind.Unspecified`**, not UTC. Without a value converter
   that marks values as UTC on read, the API serialises them **without the `Z`**, and
   the browser then treats a UTC time as local time. (`DateTimeOffset` keeps the
   offset but can't be ordered or compared in SQLite queries, as noted above.)
4. **Front-end detail:** in JavaScript, `new Date("2026-03-10")` is parsed as **UTC
   midnight**. Displayed in a UTC−x browser, that becomes 9 March. Date-only values
   must be handled as plain `yyyy-MM-dd` strings in the UI, not as `Date` objects.

**Options:**

| Option                                                                                                                                                                            | Pros                                                                                                                                                                                              | Cons                                                                                                                                                          |
| --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A. **UTC everywhere**: instants in UTC; due/application dates also stored as UTC datetimes; today = UTC date                                                                      | One rule, easy to explain; zero configuration                                                                                                                                                     | Off-by-one storage risk (1); "today" wrong for up to _offset_ hours a day (2); BR-2/BR-6 edge cases become timezone-dependent and harder to test meaningfully |
| A′. Instants in UTC; due/application dates as `DateOnly`; **today = UTC date**                                                                                                    | Fixes the storage shift (1); still zero configuration                                                                                                                                             | Still has the "today" window (2), but it's documented and predictable                                                                                         |
| **B. Hybrid: instants in UTC; due/application dates as `DateOnly` (no timezone at all); today = `TimeProvider` now → converted to one configured business timezone → `DateOnly`** | Each value has the type that matches its meaning; no storage shift; "today" matches the officers' calendar; one config value; boundaries are testable (`FakeTimeProvider` at 23:59 / 00:00 local) | One config setting to explain; still one zone for all offices                                                                                                 |
| C. B + **per-office timezone** (office on the grant or the user)                                                                                                                  | Exactly right for multi-zone programs                                                                                                                                                             | Needs an office/user concept (out of scope, Q7/Q15); more model and UI                                                                                        |
| D. **Client sends its own "today"**                                                                                                                                               | Matches each officer's local day                                                                                                                                                                  | Untrusted input for a business rule (a wrong PC clock would bypass BR-2); different answers per client for the same data                                      |

**Concrete shape of option B:**

| Kind of value  | Examples                                                                           | Type in C#                                                                                                            | Stored in SQLite             | API / JSON                                                                          |
| -------------- | ---------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- | ---------------------------- | ----------------------------------------------------------------------------------- |
| Calendar date  | `DueDate`, `ApplicationDate`                                                       | `DateOnly`                                                                                                            | text `yyyy-MM-dd` (sortable) | `"2026-03-10"`                                                                      |
| Moment in time | `CompletedAtUtc`, `CreatedAtUtc`, audit `TimestampUtc`, idempotency `CreatedAtUtc` | `DateTime` (always UTC; a value converter forces `Kind = Utc` on read)                                                | text ISO-8601 (sortable)     | `"2026-03-10T03:12:45Z"` (the `Z` is guaranteed)                                    |
| "Today"        | BR-2, BR-6, seed data                                                              | `DateOnly`, computed once per request from `TimeProvider.GetUtcNow()` + business timezone, then **passed in** (Q4 4b) | not stored                   | the UI never computes "today" for rules; it only does so for client-side form hints |

- **BR-4 consequence:** record the completion as a **UTC moment** (`CompletedAtUtc`)
  rather than only a date. That's more information for OP-4, and "the date it was
  completed" is derived by converting to the business timezone. This revises the
  earlier `CompletedOn` (`DateOnly`) idea above; the final field name is settled in
  the data-model discussion (Q7, deferred).
- **Business timezone setting:** an IANA ID in configuration (e.g.
  `"Business:TimeZone": "UTC"` or `"Australia/Sydney"`). .NET 6+ accepts IANA IDs on
  Windows and Linux.
- **Sub-question, the default value:**
  - `"UTC"`: matches your instinct and is neutral. With this default, B behaves
    exactly like A′, and switching to a real zone is a config change, not a code
    change.
  - A specific regional zone: correct "today" for that region out of the box, but
    it's an assumption about where the program runs. The brief is fictional and
    doesn't say.

**Recommendation: B, with the default `"UTC"` documented as an assumption in the
README.** This adopts your proposal for everything that is a moment in time. It
keeps calendar dates as `DateOnly`, so they can't shift, and makes "today" one
configurable business timezone. Today's behaviour is identical to "today = UTC", and
the README can honestly say how to make it right for a real region (set the zone),
or later per office (option C). Tests use an explicit non-UTC zone (e.g. UTC+10) with
`FakeTimeProvider` around midnight, to prove the conversion works rather than relying
on the default.

> **✅ Decided (2026-11-11): B (hybrid), default business timezone `"UTC"`.**
>
> - **Moments in time** are stored and returned in **UTC**. A value converter marks
>   values as UTC when read back, so JSON always carries the `Z`.
> - **Calendar dates** (`DueDate`, `ApplicationDate`) are `DateOnly`, with no
>   timezone, and are handled as plain `yyyy-MM-dd` strings in the UI.
> - **"Today"** = `TimeProvider.GetUtcNow()` converted to the configured business
>   timezone (IANA ID, default `"UTC"`), then turned into a `DateOnly` and passed into
>   rules and queries.
> - **The server never uses** `DateTime.Now`, `DateTime.Today` or `ToLocalTime()`.
> - **BR-4:** completion is recorded as a UTC moment. The field name is settled in
>   the data-model discussion (Q7).
> - **Tests** cover a non-UTC zone (e.g. UTC+10) around midnight with
>   `FakeTimeProvider`.
> - **README assumption:** "today" is the UTC date by default. Set the business
>   timezone for a real region; a per-office timezone is future work.

---

### Q9. BR-4: what does completing an already-completed task return?

**Why it matters:** the brief explicitly asks us to decide and justify this. It
interacts directly with **OP-3**: a retried "complete" (because the first response
was lost) must not look like a failure to the officer.

| Option                                                 | Pros                                                                             | Cons                                                                                              |
| ------------------------------------------------------ | -------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| **A. `200 OK` with the task, `CompletedOn` unchanged** | Idempotent by nature; a retry looks like success; the client gets the true state | A caller can't tell whether _this_ call did the completion (rarely needed)                        |
| B. `409 Conflict` "already completed"                  | Explicit                                                                         | A lost-response retry shows an error for an action that actually succeeded, which is bad for OP-3 |
| C. `204 No Content` both times                         | Simple                                                                           | Client must refetch to see `CompletedOn`                                                          |

**Recommendation: A.** Justify it as "PUT semantics: the end state is what matters,
and the operation is idempotent, which OP-3 needs".

> **✅ Decided (2026-11-11): A, `200 OK` with the task; the original completion time
> is unchanged.** The test completes the task, advances `FakeTimeProvider`, completes
> it again, and asserts that the completion time still equals the first one.

---

### Q10. Closing an already-closed grant

**Why it matters:** this is the same retry problem as Q9, for `PUT /grants/{id}/close`.

| Option                                 | Pros                           | Cons                                      |
| -------------------------------------- | ------------------------------ | ----------------------------------------- |
| **A. `200 OK` with the grant (no-op)** | Idempotent, consistent with Q9 | —                                         |
| B. `409 Conflict`                      | Explicit                       | Lost-response retry becomes a false error |

**Recommendation: A.** BR-5 still applies only when actually transitioning from open
to closed (a `409` with the reason if High-priority tasks are pending).

> **✅ Decided (2026-11-11): A, `200 OK` with the grant; a no-op if it's already
> closed.** BR-5 is checked only on the open → closed transition.

---

### Q11. Idempotency scope: which requests need a key, and is it mandatory?

**Why it matters:** your OP-3 plan persists a RequestId. Applying it everywhere costs
storage and complexity. Making it optional means some requests are unprotected.

| Option                                                                   | Pros                                                 | Cons                                                                       |
| ------------------------------------------------------------------------ | ---------------------------------------------------- | -------------------------------------------------------------------------- |
| A. Every mutating request (`POST` + `PUT`)                               | Uniform                                              | `PUT`s are already idempotent (Q9/Q10), so this is wasted work and storage |
| **B. Only `POST /grants/{id}/tasks`, the only non-idempotent operation** | Minimal; precise; easy to explain                    | Future `POST` endpoints must remember to opt in                            |
| — Key **required** on that endpoint (`400` if missing)                   | Guarantees protection; the front end always sends it | Manual `curl`/Swagger calls must add a header                              |
| — Key **optional**                                                       | Easier manual testing                                | Silent loss of protection when omitted                                     |

**Recommendation: B with the key required** on that endpoint. Document the header in
the README and in Swagger, if added.

#### Q11 follow-up ✅ _Decided_: why is `PUT` "already idempotent", and when isn't it?

**What "idempotent" means:** sending the same request **once or N times leaves the
server in the same state** (RFC 9110 §9.2.2). The _response_ may differ (e.g. the
first call transitions, the retry is a no-op); only the resulting state must match.

**Why `PUT` counts as idempotent: it's a contract, not something automatic.**

- HTTP _defines_ `PUT` as idempotent: "make the resource look like this". Clients,
  proxies and libraries are allowed to retry it automatically for that reason.
- The server implementation has to **honour** that contract; nothing enforces it.
- **Our two `PUT`s honour it because they set a target state rather than applying a
  change:**
  - `PUT /tasks/{id}/complete`: the target is "completed". Pending → completed
    records the time. Completed → completed is a no-op (BR-4 forbids changing the
    date; Q9).
  - `PUT /grants/{id}/close`: the target is "closed". Open → closed (if BR-5 passes).
    Closed → closed is a no-op (Q10).
- **The transitions are one-way:** there is no "reopen" or "un-complete". So a late
  retry can only move towards the state everyone already agreed on; it can never
  undo someone else's later change.

**When a `PUT` is _not_ idempotent, with examples:**

| #   | Pattern                                                        | Example                                                                                                                                                        | Why it breaks                                                                                              | Applies to us?                                                                                                                                                                    |
| --- | -------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Re-stamping on every call**                                  | `complete` always sets `CompletedAtUtc = now`                                                                                                                  | Each retry changes the state (and violates BR-4)                                                           | **Yes, a classic implementation bug.** Guarded by the "only write on transition" rule below and the Q9 test                                                                       |
| 2   | **Side effects on every call**                                 | `complete` inserts an `Audits` row (OP-4) or sends an email each time it's called                                                                              | The audit table is state, so a retry adds a second "completed" entry and support staff see two completions | **Yes, a real risk with OP-4 auditing.** Audit and side effects only on an actual transition                                                                                      |
| 3   | **"Last modified" bumped on no-ops**                           | `UpdatedAtUtc = now` on every `PUT` even when nothing changed                                                                                                  | Strictly a state change; misleading in investigations                                                      | Possible; same rule                                                                                                                                                               |
| 4   | **Toggle semantics**                                           | `PUT /tasks/{id}/toggle-complete`                                                                                                                              | Each call flips the state; 2 calls = back to the start                                                     | No, but a likely live-change trap ("add an undo button")                                                                                                                          |
| 5   | **Relative / incremental update**                              | `PUT /tasks/{id}/due-date { "addDays": 7 }`, `PUT /grants/{id}/counter { "increment": 1 }`                                                                     | The outcome depends on how many times it was applied                                                       | No, but a live change like "postpone a task by N days" must send the **absolute** new date, or become a `POST` with a key                                                         |
| 6   | **`PUT` that creates with a server-generated id**              | `PUT /tasks` (no id in the URL) creates a new task each time                                                                                                   | That's `POST` semantics wearing a `PUT` verb                                                               | No                                                                                                                                                                                |
| 7   | **Retry interleaved with another user's change** (lost update) | Hypothetical "reopen task" endpoint: Officer A completes (response lost) → Officer B reopens → A's retry arrives and completes it again, silently overriding B | Idempotency covers _the same request repeated_, not _other requests in between_                            | **Not today**, because transitions are one-way. If a reopen/edit endpoint is added, it needs conditional requests (`If-Match` + ETag / row version), which is also OP-2 territory |

**Non-examples (still idempotent):**

- **A different response on retry:** e.g. the first `close` returns `409` (a High task
  is pending), someone completes that task, and the retry returns `200`. Each request
  correctly reflects the state at that time.
- **Log lines written on every call:** logs are observation, not business state. That
  matches the OP-4 split: logs record every _attempt_, audits record every _change_.

**Rules this adds (if accepted):**

1. **State-changing `PUT`s use a "transition guard":** check the current state; if
   it's already the target, return `200` with the current resource and write nothing
   (no field, no audit row, no side effect).
2. **Audits record state changes, not requests.** Every call is still logged (OP-4).
3. **Toggle, relative or create-style operations are never `PUT`.** They become
   absolute `PUT`s, or `POST` + idempotency key.
4. **Tests:** for each `PUT`, call it twice (advancing `FakeTimeProvider` in between)
   and assert the state and the audit row count are unchanged after the second call.

**Recommendation (unchanged): B + key required**, plus rules 1–4 above. They're what
makes "`PUT` is already idempotent" true in our code rather than just assumed, and
they keep it true during the live change.

> **✅ Decided (2026-11-11): a mix of A and B, a reusable opt-in mechanism.**
>
> - **A's uniformity:** idempotency is a **single reusable mechanism**, not code
>   inside one handler. Any endpoint or command can opt in declaratively, the way an
>   attribute or marker would, without copying logic.
> - **B's scope:** today **only `POST /grants/{id}/tasks`** opts in. The `PUT`s don't
>   need it (Q9/Q10, rules 1–4).
> - **Key required where opted in:** an opted-in request without a key gets `400`
>   ProblemDetails. Requests that haven't opted in ignore the header.
> - **Rules 1–4 above** are carried forward as recommended. _Assumption, since you
>   didn't object; say if any should be dropped._
> - **The concrete form** (middleware, endpoint filter + attribute, or mediator
>   pipeline behaviour + marker) is decided in **Q13**, because it decides whether
>   the key and the task can be saved atomically.

---

### Q12. Idempotency storage semantics

**Why it matters:** a naive "save the RequestId, skip if seen" design still allows
duplicates under concurrency, gives the wrong answer on replay, and accepts misuse
of keys.

Decisions bundled here:

1. **What is stored:** key, route/operation, a hash of the request body, the response
   status and response body (or the created task id), and a creation time.
2. **Replay with the same key and the same body:** return the **stored response**
   (same `201` and the same task), not a new one, and not an error.
3. **Same key with a different body:** this is a client bug. Options are `422` (common,
   e.g. Stripe-style) or `409`. **Recommend `422`** with a clear ProblemDetails.
4. **Two retries arriving at the same time:** a **unique index on the key**, with the
   key and the task saved **in the same transaction**. The loser hits the constraint
   and returns the stored response. **Recommended**: without it, two concurrent
   retries could both create tasks.
5. **Store failed responses?**
   - _Store only successes; reprocess failures._ Simple; a retry after a BR-1/BR-2
     failure is re-evaluated against current state. **Recommended.**
   - _Store all responses (Stripe-style)._ Strict replay, but a stale error can be
     replayed after the state changed.
6. **Expiry:** a TTL (e.g. 24 hours) plus cleanup. **Recommend documenting the TTL and
   deferring the cleanup job** (README "next steps").

**Recommendation:** as marked above.

> **✅ Decided (2026-11-11), points 3–6:**
>
> - **(3) Same key, different body:** `422` ProblemDetails.
> - **(4) Concurrent retries:** a unique index on the key, with the key and the task
>   saved in the same transaction.
> - **(5) Successes only:** failures are not stored, and a retry after a failure is
>   re-evaluated against the current state.
> - **(6) Expiry:** **no cleanup is built for now**, but the README documents the
>   intended TTL (e.g. 24 hours), the unbounded table growth, and the planned
>   cleanup approach (a scheduled job or a startup sweep deleting keys older than the
>   TTL). It is also listed in section 5, Known limitations.
>
> Points 1–2 were discussed below and are now decided (B).

#### Q12 follow-up ✅ _Decided_: why store the body hash, response status and response body?

Each stored field exists to answer one specific question when a request with a known
key arrives:

| Field                                | The question it answers                                                                    | What happens without it                                                                                                                                                                                                                                                                                                                     |
| ------------------------------------ | ------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Key**                              | "Have I seen this request before?"                                                         | No idempotency at all                                                                                                                                                                                                                                                                                                                       |
| **Operation** (command type / route) | "Is the key being reused for a _different kind_ of action?"                                | A key first used on "add task" could replay a task in response to a different future endpoint                                                                                                                                                                                                                                               |
| **Request hash**                     | "Is this really the _same_ request, or a client bug reusing a key for new data?" (point 3) | **Silent data loss.** The front end accidentally reuses the key for a second, different task ("Verify bank details", then "Schedule site visit"). The server sees a known key, replays the first task, and returns `201`. The officer believes the second task exists, but it was never created. With the hash, this becomes a clear `422`. |
| **Response status**                  | "What status do I return on replay?"                                                       | Not needed **for us**: we only store successes (point 5), so the status is always the success status (`201`).                                                                                                                                                                                                                               |
| **Response body**                    | "What exactly do I send back on replay?" (point 2)                                         | The replay can't return the created task, so the front end gets nothing to show or put in its cache, or the server needs per-endpoint code to rebuild it                                                                                                                                                                                    |

**Why a hash and not the full body:**

- It has a fixed size (SHA-256 = 32 bytes), whatever the payload size.
- It doesn't store officer-entered data a second time.
- Equality is all that's needed.

**What to hash** (follows from Q13 D/C, where the check sits in the mediator
pipeline): hash the **command object** (which includes `GrantId` from the route, the
title, due date and priority, but **not** the key itself), serialised in a fixed
property order, rather than the raw HTTP bytes. Otherwise harmless differences
(whitespace, JSON property order) would cause false `422`s. Including `GrantId`
means "the same body sent to another grant" counts as a different request.

**Options for what to store as the "response":**

| Option                               | Stores                                                                      | Pros                                                                                                                                                                                                                                                                             | Cons                                                                                                                                                                       |
| ------------------------------------ | --------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A. Full HTTP snapshot (Stripe-style) | Status + headers + HTTP body                                                | Exact byte-for-byte replay; works for any endpoint                                                                                                                                                                                                                               | Only possible at middleware level (rejected in Q13); the stored JSON shape goes stale if the DTO changes; stores the most data                                             |
| **B. Handler result, serialised**    | The command's result object (e.g. the created task DTO), as JSON; no status | **Generic**: the pipeline behaviour stores and replays _any_ opted-in command's result without per-command code, which matches Q11's "reusable" goal. Status is implied (success only). Replays the **original** result, which is the honest answer to "what did my request do?" | Same stale-shape risk if the result type changes between deploys (acceptable with a short TTL); stores a small JSON blob                                                   |
| C. Created resource id only          | e.g. `TaskId`; on replay, re-read the resource and return its current state | Smallest; replay shows fresh data (e.g. already completed)                                                                                                                                                                                                                       | **Not generic**: every opted-in command needs its own "how to re-read" code, against Q11. Breaks if the resource is later deleted.                                         |
| D. Key + operation only              | Nothing to replay                                                           | Smallest                                                                                                                                                                                                                                                                         | A retry has to return an error ("already processed") or an empty response. That's exactly the "looks like a failure but actually succeeded" problem OP-3 asks us to avoid. |

**Recommendation: B.** Store **key, operation, request hash, the serialised handler
result, and `CreatedAtUtc`**. **Don't** store the HTTP status or headers.

| Request arriving with a known key | Response                                               |
| --------------------------------- | ------------------------------------------------------ |
| Same operation and same hash      | Replay the stored result as `201`                      |
| Same key, different hash          | `422`                                                  |
| Key not seen before               | Run normally, store the result in the same transaction |

This revises point 1's original wording: "the response status and response body"
becomes "the handler's result", because only successes are stored.

> **✅ Decided (2026-11-11): B.**
>
> - **Stored:** key, operation, request hash (SHA-256 of the command in a fixed
>   serialisation order, excluding the key, including `GrantId`), the serialised
>   handler result, and `CreatedAtUtc`.
> - **Not stored:** the HTTP status or headers.
> - **Replay:** same operation and hash → the stored result as `201`; different hash
>   → `422`.

**Why it matters:** atomicity. If the key is saved outside the transaction that
creates the task, a crash between the two leaves a task without a key (a duplicate
on retry) or a key without a task (lost work). This also connects to Q2.

| Option                                                                                                                | Pros                                                   | Cons                                                                                             |
| --------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------ | ------------------------------------------------------------------------------------------------ |
| A. ASP.NET middleware / endpoint filter caching the HTTP response                                                     | Generic; transport-level                               | Runs outside the business transaction; caches raw HTTP; harder to make atomic with SQLite writes |
| **B. Inside the use case (handler, or a mediator pipeline behaviour if Q2 = C), sharing the `DbContext` transaction** | Atomic with the task insert; easy to test at API level | Needs a small reusable piece so future commands can opt in                                       |

**Recommendation: B.** If a hand-rolled mediator is chosen (Q2 = C), this is its
strongest justification.

> **Update (2026-11-11):** Q2 is decided as C, so B now concretely means **an
> idempotency pipeline behaviour** in the hand-rolled mediator. It opts in per command
> (e.g. via a marker interface on `AddTaskCommand`) and runs the key lookup, the
> handler and the key insert in **one `DbContext` transaction**. Still to confirm
> together with Q11/Q12.

#### Q13 follow-up ✅ _Decided_: which reusable form? (input from Q11)

Q11 asks for a **reusable, opt-in** mechanism ("a middleware or attribute or
service"). All of the options below are reusable and opt-in. They differ in **where
the work happens**, and therefore in whether the key and the task are saved
**atomically** (one transaction) and whether the business logic stays HTTP-free.

The job splits into two halves:

- **HTTP half:** read the `Idempotency-Key` header, reject it if missing, and turn a
  replay or a mismatch into a proper HTTP response.
- **Data half:** look up the key, run the handler, then save the key and the
  response **in the same transaction** as the task.

| Option                                                                               | How you opt in                                                                       | Pros                                                                                                                                                                                                                                                               | Cons                                                                                                                                                                                              |
| ------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A. **ASP.NET middleware** that caches the HTTP response by key                       | A route list or endpoint metadata                                                    | Fully generic; transport-level                                                                                                                                                                                                                                     | Runs **outside** the business transaction: a crash between "task saved" and "key saved" means a duplicate on retry. Has to buffer the response body. Can't use the unique-index race fix cleanly. |
| B. **Endpoint filter + `[Idempotent]` attribute/metadata** doing both halves         | `.AddEndpointFilter<IdempotencyFilter>()` or an attribute on the endpoint            | Declarative, visible on the endpoint; standard ASP.NET                                                                                                                                                                                                             | Same atomicity problem as A (the filter's write isn't in the handler's transaction unless the filter owns the transaction, which pulls data access into the API layer)                            |
| C. **Mediator pipeline behaviour + marker interface** doing both halves              | `AddTaskCommand : IIdempotentCommand` (carries the key)                              | **Atomic**: the behaviour opens the transaction, runs the handler, saves the key and commits once. Testable in `Core.Tests` without HTTP. Uses the mediator decided in Q2.                                                                                         | The key must travel inside the command, so the endpoint still has to copy the header into it                                                                                                      |
| **D. Hybrid: a thin endpoint filter (HTTP half) + a pipeline behaviour (data half)** | Endpoint: `[Idempotent]` / `.RequireIdempotencyKey()`. Command: `IIdempotentCommand` | Each half lives in the layer it belongs to. **Atomic** (C's strength). Declarative on the endpoint (A/B's strength). Missing key → `400` before any business code runs. Each half is reusable, and the two can be tested separately (`Api.Tests` and `Core.Tests`) | Two small pieces to explain instead of one, and both must be applied together (one integration test guards this)                                                                                  |

**Recommendation: D.**

- It delivers exactly what Q11 asks for: uniform, reusable, attribute-like opt-in,
  applied only to `POST /grants/{id}/tasks` today.
- It keeps the strongest property of the earlier recommendation: the key and the task
  are committed in **one transaction**, which is what makes concurrent retries safe
  (Q12, point 4).
- It also gives the hand-rolled mediator a real job (the Q2 guardrail).
- **If you'd rather have one piece,** choose C and let the endpoint copy the header
  into the command. Avoid A and B on their own: they can't give atomicity without
  pushing data access into the API layer.

> **✅ Decided (2026-11-11): C, a mediator pipeline behaviour with a marker
> interface (one piece).**
>
> - **Opt-in:** a command opts in by implementing the marker interface (e.g.
>   `AddTaskCommand : IIdempotentCommand`), which carries the key.
> - **Endpoint:** copies the `Idempotency-Key` header into the command. That is its
>   only idempotency-related code.
> - **Behaviour:**
>   - validates that the key is present (missing → validation error → `400`
>     ProblemDetails, through the normal error mapping);
>   - computes the request hash (Q12);
>   - opens the transaction, looks up the key, runs the handler, saves the key and
>     the result, and commits **once**;
>   - on the unique-index race, returns the stored result.
> - **Why C over D:** one piece to explain and keep correct, and a hand-rolled
>   mediator that does real work (Q2 guardrail).
>   - Accepted trade-off: the endpoint must remember to copy the header.
>   - Guard: an API test that sends the add-task request without the header and
>     expects `400`.
> - **Tests:**
>   - `Core.Tests`: replay, hash mismatch, missing key, race path via a Moq'd
>     unique-violation.
>   - `Data.Tests`: the unique index.
>   - `Api.Tests`: an end-to-end duplicate POST creates one task (the OP-3 risk
>     test).

---

### Q14. OP-4 scope: what does "investigate days later" need?

**Why it matters:** support staff who didn't build the tool need to connect "an
officer said X failed on Tuesday" to concrete evidence. The default console logs
disappear on restart.

Building blocks (not mutually exclusive):

| Option                                                                                                                                       | Pros                                                        | Cons                                                                          |
| -------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------- | ----------------------------------------------------------------------------- |
| **A. Structured logs persisted to a rolling JSON file (Serilog: common, Apache-2.0)**                                                        | Survives restarts; searchable; widely used                  | New dependency (needs approval); file logs aren't central across API copies   |
| B. Built-in `ILogger` JSON console only                                                                                                      | No dependency                                               | Lost unless something collects stdout; nothing to search days later in a PoC  |
| C. Logs into a SQLite table                                                                                                                  | One place for everything                                    | Logging competes with app writes in a single-writer database; unusual pattern |
| **D. Correlation: ASP.NET `traceId` in every ProblemDetails, in every log line, and shown in the UI error message so officers can quote it** | Direct link from a user report to the logs; cheap           | —                                                                             |
| **E. Audit trail (`Audits` table) for state changes: task created/completed, grant closed, with timestamp and traceId**                      | Answers "what happened to this grant?" without reading logs | Must be written in the same transaction as the change                         |

**Recommendation: A + D + E.** Do not add a log viewer or OpenTelemetry in the PoC;
list central logging (e.g. Seq, Application Insights, OTel) as a next step.

> **✅ Decided (2026-11-11): E is mandatory, for state changes _and_ user actions.**
> What exactly "user action" covers is sub-question 14.2 below. A and D were
> discussed and decided below.

> **✅ Decided (2026-11-11): A + D + E, with U2.** All Q14 recommendations accepted.
>
> - **A:** Serilog with a rolling JSON file sink. The dependency is approved;
>   `Serilog.AspNetCore` plus a file sink, with exact packages pinned in `plan.md`.
> - **D:**
>   - The traceId is in every ProblemDetails, on every log line (captured
>     automatically) and on every audit row.
>   - The UI shows an 8-character "Reference" with a copy-full button.
>   - The idempotency key, user id and entity ids are added as log properties.
> - **E + U2:** an audit pipeline behaviour around every command records one row per
>   action, with `Outcome` = `Changed` / `NoChange` / `Replayed` / `Rejected` (+ rule
>   code).
>   - **Success:** the row is written in the same transaction as the change.
>   - **Rejection:** the row is written in a separate transaction after the rollback.
>   - **Queries:** not audited.

#### Q14 follow-up ✅ _Decided_

##### 14.1 What does "traceId" in option D mean?

**What it is:** a unique id that ASP.NET Core gives **every HTTP request**.

- It follows the **W3C Trace Context** standard: a 32-hex-character trace id such as
  `4bf92f3577b34da6a3ce929d0e0e4736`, carried in the `traceparent` header format
  `00-<trace-id>-<span-id>-01`.
- It comes from `Activity.Current`, with `HttpContext.TraceIdentifier` as a fallback.
- **It needs no library:**
  - `AddProblemDetails()` already adds a `traceId` field to every ProblemDetails
    error response.
  - The built-in logging can stamp the same id onto **every log line written while
    handling that request**.

**What it's for:** it's the **thread that ties together everything about one
request**: the error the officer saw, every log line, the exception stack trace, and
the audit row.

**A support scenario (OP-4), with and without it:**

| Step                            | Without traceId                                                                                            | With traceId                                                                                                                         |
| ------------------------------- | ---------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| Officer sees an error           | "Something went wrong"                                                                                     | "Couldn't add the task. Reference: `4bf92f35…`" (with a copy button)                                                                 |
| Officer reports it 3 days later | "It failed on Tuesday afternoon, I think on the Riverside grant"                                           | Pastes the reference                                                                                                                 |
| Support investigates            | Searches the logs by an approximate time, guessing which of hundreds of requests from ~100 officers it was | Searches the logs for the id: the exact request, inputs, rule violation or exception, timings, and the audit row if anything changed |

**How it fits with what's already decided:**

- **Retries (OP-3, Q11–Q13):** each HTTP attempt gets its **own** traceId, but every
  retry of one user action shares the **same idempotency key**. Logging both lets
  support see "this action was tried 3 times; attempt 2 created the task; attempt 3
  replayed it". The two ids complement each other.
- **Audit (E):** storing the traceId on each audit row links "what changed" (audit)
  to "why and how" (logs).
- **Server unreachable (OP-3 client half):** the request never arrived, so there is
  **no server traceId**. The UI shows the "can't reach the server" banner instead.
  For add-task, the idempotency key the client generated is the only reference.
  - _Possible extra (not recommended for now):_ the client sends its own
    `traceparent` header, so even failed attempts have a client-side id. It adds
    front-end code for little PoC value.
- **Q16 interaction:** ProblemDetails carries `traceId` automatically. Q16's
  recommendation adds a stable `code` (e.g. `"BR-1"`) next to it.

**Display options in the UI:**

| Option                                                                                  | Pros                                                               | Cons                                                                                     |
| --------------------------------------------------------------------------------------- | ------------------------------------------------------------------ | ---------------------------------------------------------------------------------------- |
| Full id (32 characters)                                                                 | Unambiguous                                                        | Long and intimidating for officers                                                       |
| **The first 8 characters as a "Reference", with a copy button that copies the full id** | Easy to read out over the phone; the full id is available to paste | A collision on 8 characters is very unlikely but possible; support can narrow it by date |
| Don't show it                                                                           | Cleanest UI                                                        | Breaks the main link between the officer's report and the logs                           |

**Recommendation for D:** keep it. It's nearly free (built into ASP.NET Core) and is
the single most useful thing for "investigate days later".

- ProblemDetails includes `traceId`.
- Every log line includes `TraceId`; the idempotency key is logged when present.
- Audit rows store `TraceId`.
- The UI shows an 8-character reference with copy-full.

**Clarification (2026-11-11): "So the traceId is what gets recorded in the Serilog
log?"** Yes, with one nuance: **ASP.NET Core creates the id, and Serilog records it.**

1. **The request arrives.** ASP.NET Core starts an `Activity` for it, which holds the
   W3C trace id. If the caller sent a `traceparent` header, that id is continued
   instead.
2. **Any code logs during that request** (endpoint, mediator behaviours, handlers,
   EF Core). **Serilog 3.1+ automatically captures `Activity.Current`'s trace and
   span ids on every log event.** No manual code is needed.
3. **The JSON file sink writes them as fields on every line.** For example (compact
   JSON format: `@tr` = trace id, `@sp` = span id):
   ```json
   {
     "@t": "2026-11-11T09:12:45.1Z",
     "@mt": "Business rule {RuleCode} rejected {Command}",
     "RuleCode": "BR-5",
     "Command": "CloseGrantCommand",
     "GrantId": "…",
     "UserId": "…",
     "@tr": "4bf92f3577b34da6a3ce929d0e0e4736",
     "@sp": "00f067aa0ba902b7"
   }
   ```
4. **One summary line per request** (`UseSerilogRequestLogging`: method, path, status
   code, elapsed time) carries the same `@tr`.
5. **We add context properties** from a mediator behaviour or middleware: the user id
   (Q15), the idempotency key (when present), and the command name and entity ids. So
   a line can be found by trace id _or_ user _or_ grant.
6. **The same trace id is in the ProblemDetails `traceId`** (and the UI reference)
   and in the **audit row**. One search in the log file then finds every line for
   that request.

If Serilog isn't approved, the built-in JSON console logger can include the trace id
too (via logging scopes), but **nothing persists it to a file**, so option A (or
another file sink) is what makes "days later" possible.

##### 14.2 What counts as a "user action" for the audit?

You made the audit mandatory for **state changes and user actions**. This needs a
precise meaning, because it interacts with the Q11 rule ("a repeated `PUT` must not
create a second state-change record") and with rejected requests (which roll back
their transaction).

| Option                                                    | Audit contains                                                                                                                                                                                      | Pros                                                                                                                                                                                                                                           | Cons                                                                                                                                                                                                    |
| --------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| U1. State changes only                                    | One row per actual change (task created, task completed, grant closed). Attempts, rejections and retries go to the logs only                                                                        | Simplest; written in the same transaction as the change                                                                                                                                                                                        | Doesn't meet "user actions" as you stated it: a rejected close (BR-5) leaves no audit trace                                                                                                             |
| **U2. Every state-changing user action, with an outcome** | One row per **command** (add task, complete, close) with `Outcome` = `Changed` / `NoChange` (already in the target state) / `Replayed` (idempotent retry) / `Rejected` (+ rule code such as `BR-5`) | Meets "state changes _and_ user actions"; answers "the officer says they tried to close it on Tuesday and it refused" from the database alone; still no false "completed twice", because a retry shows as `NoChange`/`Replayed`, not `Changed` | **Rejected** actions roll back the business transaction, so their audit row must be written **separately** after the rollback. If that write also fails, the logs are the fallback. Slightly more rows. |
| U3. U2 + reads (who viewed which grant)                   | Every request                                                                                                                                                                                       | Full access trail                                                                                                                                                                                                                              | Noisy; useless without identity (Q15); heavy for a PoC                                                                                                                                                  |

**Recommendation: U2.** Implement it as an **audit pipeline behaviour** in the
hand-rolled mediator, around every command (not queries):

- **Success:** the audit row is written in the same transaction as the change, so
  it's atomic.
- **Failure:** the row is written in a fresh, small transaction after the rollback.

This revises Q11 rule 2 to: "**`Changed` rows** only on an actual transition;
repeats are recorded as `NoChange`/`Replayed`". The "who" column comes from Q15
(decided: the signed-in user).

---

### Q15. Identity without authentication: who goes into the audit?

**Why it matters:** OP-4 investigations usually ask "who did this?". The brief has no
login, and the drafted `Audits.ById` FK to `Users` can't be filled honestly.

| Option                                                                                                                      | Pros                     | Cons                                                            |
| --------------------------------------------------------------------------------------------------------------------------- | ------------------------ | --------------------------------------------------------------- |
| A. Real authentication (e.g. Entra ID / Identity)                                                                           | Trustworthy "who"        | Far out of scope                                                |
| B. Unauthenticated "acting officer" picker sent as a header                                                                 | Some "who" information   | Spoofable; adds UI and model scope; could mislead investigators |
| **C. No user in the PoC; audit records action, entity, timestamp, traceId (+ optionally client IP / user agent from logs)** | Honest; no fake identity | Can't answer "who" until auth exists                            |

**Recommendation: C.** Make the user column nullable or remove it, and record
authentication plus per-user audit as the first "before going live" item.

#### Q15 follow-up ✅ _Decided (sign-in UI deferred to planning)_: your proposal, a simple users table plus email "login"

**Proposal (2026-11-11):**

- A `Users` table (`Id`, `Name`, `Email`) seeded with **5 users**.
- "Login" = entering an email, **with no password**.
- Purpose: give the audit a "who" (Q14 E is mandatory).

**Assessment: sound, provided it is framed honestly.** It's a refined version of
option B, **identification, not authentication**.

- It makes the mandatory audit far more useful: "who completed this?" now has an
  answer.
- It's small (one table, one endpoint, one middleware) and demonstrable in the demo.
- It mirrors real life, where officers would log in.
- **The one risk** is a reviewer reading it as "fake security". The defence is to
  name it plainly in the README ("identification for audit context, not
  authentication; anyone can claim any seeded email") and to list real
  authentication (e.g. Entra ID / OIDC) as the first "before going live" item.

**Design points to settle:**

**(a) How does the server know the user on each request?** This must work with
several API copies (OP-1) and with retries (OP-3).

| Option                                                                                                                                                    | Pros                                                                                  | Cons                                                                                                            |
| --------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| Server-side session (cookie → in-memory session)                                                                                                          | Familiar                                                                              | **Breaks OP-1:** a session lives in one API copy's memory; a restart or another copy means "logged out"         |
| **Stateless header: the client sends `X-User-Id` on every request; middleware loads the user from the database (unknown/missing → `401` ProblemDetails)** | Works across copies and restarts; trivial; easy to test (`Api.Tests` sets the header) | Trivially spoofable, but so is the email login itself, so it's no weaker                                        |
| Signed token (JWT) issued at "login"                                                                                                                      | Industry-shaped; tamper-proof once issued                                             | Tamper-proofing adds nothing when getting a token only needs a known email; signing-key config; more to explain |

**(b) What does "login" look like?** The brief says **one screen** is enough.

| Option                                                                                                                                                                                                                                                | Pros                                               | Cons                                                                             |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------- | -------------------------------------------------------------------------------- |
| A separate login page/route                                                                                                                                                                                                                           | Realistic                                          | A second screen and routing for no functional gain                               |
| **A "Sign in as: [email]" box in the header of the same screen.** `POST /session { email }` returns the user (`401` if unknown). The user is kept in the Redux store and `localStorage` so a page reload keeps it. A "Switch user" link is available. | Stays one screen; quick to demo different officers | `localStorage` is readable by scripts (irrelevant here: there's no secret in it) |
| A drop-down of the 5 users instead of typing an email                                                                                                                                                                                                 | Simplest UI; no "unknown email" errors             | Lists every user to anyone; looks less like a login                              |

**(c) Which endpoints require a user?**

- **Recommended:** all API endpoints except `POST /session` (and a future
  `/health`). One simple rule. Unknown or missing user → `401`.
- Alternative: commands only. Reads stay open, but that's two rules to explain.

**(d) Data and seed details:**

- `Email` has a **unique index** and is compared **case-insensitively** (normalised
  to lowercase on save and lookup).
- Seed 5 **fictional** users on the reserved **`example.org`** domain (e.g.
  `alex.nguyen@example.org`), so no real address is ever used.
- `Audits.UserId` becomes **not null** for command audits. Requests without a user
  are rejected with `401` before any command runs, so there is always a "who".
- The `Users` table re-enters the data model. Assignments (`AssignUserTask` /
  `AssignUserGrant`) stay with the deferred Q7 discussion.

**(e) Where does it live in the architecture?**

- **Api:** middleware resolves the header to a user.
- **Core:** an `ICurrentUser` abstraction, read by the audit pipeline behaviour
  (Q14.2) and added to log context (Q14.1).
- **Tests:**
  - `Api.Tests`: missing/unknown user → `401`; login with a mixed-case email.
  - `Core.Tests`: the audit row carries the current user.
  - `Data.Tests`: unique email.

**Recommendation: accept the proposal** with (a) a stateless `X-User-Id` header, (b)
an inline "Sign in as" box on the same screen, (c) a user required on all endpoints
except `/session`, (d) a unique case-insensitive email and fictional `example.org`
seed users, and an honest README label. This replaces the earlier recommendation C
above.

> **✅ Decided (2026-11-11): proposal accepted with (a), (d), (e) and the honest README
> label.**
>
> - **(b) Sign-in UI:** ⏸ deferred. It will be designed and decided in `plan.md`.
> - **(c) Exempt endpoints:** decided below (only `POST /auth/login`).

##### Q15(c) clarification: what are `POST /session` and `/health`?

**`POST /session` is the sign-in endpoint proposed in (b).**

- **Request:** `{ "email": "alex.nguyen@example.org" }`.
- **Server:** looks the email up (case-insensitive) and returns
  `{ id, name, email }`, or `401` ProblemDetails if the email is unknown.
- **Client:** keeps that user and sends `X-User-Id` on every later request.
- **Why it must be exempt from "user required":** it's the chicken-and-egg step.
  The client can't present a user id before it has signed in to get one.
- **Why `POST`:** it keeps the email in the request body rather than the URL. URLs
  end up in logs, browser history and proxies, and an email is personal data.
- **About the name:** "session" is conventional REST wording for "start a sign-in",
  but it's slightly misleading here, because **no session is stored on the server**
  (the design is stateless, per (a)). Clearer alternatives include
  `POST /auth/sign-in` or `POST /users/identify`. The exact route belongs to the
  sign-in design (b), so it's settled in `plan.md` along with it.

**`/health` is the health-check endpoint from OP-5.**

- **What it does:** a `GET` that reports whether this API copy is running and can
  reach its database. It's built into ASP.NET Core (`AddHealthChecks()` +
  `AddDbContextCheck<…>()` + `MapHealthChecks("/health")`) and returns `200
Healthy` or `503 Unhealthy`.
- **Who calls it:** **load balancers, container orchestrators and monitoring
  tools**, not officers. They poll it every few seconds to decide whether to send
  traffic to a copy (OP-1/OP-5).
- **Why it must be exempt:** those tools have no officer identity. Requiring
  `X-User-Id` would make every copy look "unhealthy" and take it out of service.
- **Is it being built?** **Not decided.** OP-5 isn't one of the two chosen needs; Q19
  lists it as an optional ~10-minute bonus after the core is done. It was mentioned
  in (c) only so the rule is written correctly if it's added.

**Restated rule for (c):** every API endpoint requires a known user, **except**:

- the sign-in endpoint (route name decided with (b));
- infrastructure endpoints with no human caller: `/health` if OP-5 is built, and
  Swagger/OpenAPI documentation if added.

**Recommendation:** accept this restated rule.

> **✅ Decided (2026-11-11):**
>
> - **Sign-in route:** `POST /auth/login` (body `{ "email": "…" }` → the user, or
>   `401`).
> - **Rule:** every API endpoint requires a known user (`X-User-Id`) **except
>   `POST /auth/login`**.
> - **Out of scope:** `/health` (OP-5) and other OP-driven endpoints are outside this
>   scope (see Q19), so no further exemptions are defined now. If one is added later,
>   the rule is extended then.
> - **Still deferred:** the sign-in **UI** (b) is designed in `plan.md`.

---

### Q16. Error contract: which status codes for which rule? ✅ _Decided_

**Why it matters:** "consistent error body and appropriate status codes" is graded.
The front end must tell **validation**, **business-rule**, **not found** and
**unreachable** apart (OP-3).

| Option                                                                                                                                                                                                                  | Pros                                                | Cons                                                                 |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------- | -------------------------------------------------------------------- |
| A. Everything `400`                                                                                                                                                                                                     | Simple                                              | The front end can't distinguish input mistakes from state conflicts  |
| **B. `400` (ValidationProblemDetails) for request input: BR-3 title, BR-2 dates; `409` for state conflicts: BR-1 closed grant, BR-5 pending High tasks; `404` for unknown ids; `422` for idempotency-key misuse (Q12)** | Matches common REST usage; meaningful to the client | Teams debate `400` vs `422` for validation, so be ready to defend it |
| C. `422` for all business rules                                                                                                                                                                                         | Some teams prefer it                                | Mixes "bad input" with "valid input, wrong state"                    |

**Recommendation: B.** Every error is ProblemDetails with a stable machine-readable
`code` extension (e.g. `"BR-1"`) and the `traceId` (for OP-4). The front end maps
`code` to a message and shows `traceId`.

> **✅ Decided (2026-11-11): B.**
>
> | Status                           | Used for                                                    |
> | -------------------------------- | ----------------------------------------------------------- |
> | `400` (ValidationProblemDetails) | Request-input errors: BR-2, BR-3, a missing idempotency key |
> | `401`                            | Unknown or missing user (Q15)                               |
> | `404`                            | Unknown ids                                                 |
> | `409`                            | State conflicts: BR-1, BR-5                                 |
> | `422`                            | Idempotency-key misuse (Q12)                                |
>
> Every error is ProblemDetails with a stable `code` (e.g. `"BR-1"`) and `traceId`.

---

### Q17. Front-end behaviour when the network is unreliable (OP-3, client half) ✅ _Decided_

**Why it matters:** OP-3 requires that the screen **tells officers clearly** when the
server can't be reached. `fetch` has **no default timeout**, so a hung request can
look like "nothing happens". RTK Query retries can also re-send mutations, which is
only safe with the right key handling.

Decisions bundled here:

1. **Timeout:** set a `fetchBaseQuery` timeout (e.g. 10 seconds), so a hang becomes a
   visible `TIMEOUT_ERROR`. **Recommended.**
2. **Unreachable indicator:** a persistent banner on `FETCH_ERROR`/`TIMEOUT_ERROR`
   (optionally also `navigator.onLine` events), distinct from inline ProblemDetails
   errors. **Recommended.**
3. **Retries:**
   - _Queries (GET):_ automatic retry with backoff + `refetchOnReconnect`.
     **Recommended.**
   - _Mutations:_ either automatic retry (safe only because of the key) or a manual
     "Try again" button. **Recommend manual "Try again"**, so the officer stays in
     control and sees what happened.
4. **Key lifetime:** generate the idempotency key **once per user intent** (on form
   submit), pass it as a mutation argument, and **reuse it on every retry** of that
   intent. Clear it only after success. **Never generate it in `prepareHeaders`**,
   because that creates a new key per attempt and defeats OP-3.

**Recommendation:** as marked above.

> **✅ Decided (2026-11-11): all four as recommended.**
>
> - **Timeout:** about 10 seconds on `fetchBaseQuery`.
> - **Unreachable banner:** a persistent banner on `FETCH_ERROR`/`TIMEOUT_ERROR`,
>   separate from inline ProblemDetails errors.
> - **Retries:**
>   - Queries: automatic retry with backoff, plus `refetchOnReconnect`.
>   - Mutations: a manual "Try again" button.
> - **Idempotency key:** created once per user intent, reused on every retry of that
>   intent, and never generated in `prepareHeaders`.

---

### Q18. Database lifecycle: `EnsureCreated` or migrations, and when to seed? ✅ _Decided_

**Why it matters:** the brief wants seed data relative to the **app start date** and
a README that works on the grader's machine. If the SQLite file persists across runs,
dates seeded on day 1 drift (tasks become "more overdue"). Recreating the database on
every start loses data, which contradicts the spirit of OP-1.

| Option                                                                                                                   | Pros                                                                                                                                                                          | Cons                                                                     |
| ------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| A. `EnsureCreated` + reseed every start (delete the DB file)                                                             | Demo always fresh                                                                                                                                                             | Loses all work on restart; unrealistic                                   |
| B. `EnsureCreated` + seed only if empty                                                                                  | Simple                                                                                                                                                                        | No schema evolution path; `EnsureCreated` and migrations don't mix later |
| **C. EF Core migrations applied at startup + seed only if empty; README explains deleting the DB file for a fresh demo** | Realistic; schema changes during the live change are easy and visible; the README "before running for real" note is natural (run migrations in a deploy step, not at startup) | Slightly more setup                                                      |

**Recommendation: C.** Tests use their own in-memory SQLite database and their own
seed with a `FakeTimeProvider`, so they never depend on the real date.

> **✅ Decided (2026-11-11): C.**
>
> - EF Core migrations are applied at startup.
> - Seed data (users, grants, tasks relative to "today") is added only if the
>   database is empty.
> - The README explains how to delete the database file for a fresh demo, and that
>   in production migrations would run as a deploy step.
> - Tests build their own in-memory database and seed.

---

### Q19. Which OP needs to build: OP-3 + OP-4 only, or also OP-5? ✅ _Decided_

**Why it matters:** "Operations needs" is 10% and rewards _sensible choices built
well_ plus proportionate explanations of the rest.

| Option                                                                   | Pros                                                                           | Cons                                                     |
| ------------------------------------------------------------------------ | ------------------------------------------------------------------------------ | -------------------------------------------------------- |
| **A. OP-3 + OP-4** (decided)                                             | Highest day-one impact (flaky networks, support investigations); both testable | —                                                        |
| B. A + OP-5 (`/health` with a database check via built-in health checks) | ~10 minutes; directly answers "can it reach its data"                          | Time taken from the core; must still be tested/explained |

**Recommendation: A for the base. Add B only after the core and tests are green.** In
the README, address OP-2's real risk explicitly: concurrent "add High task" vs
"close grant" could break BR-5. SQLite's single writer masks this; a server database
would need a transaction with appropriate isolation or optimistic concurrency.

> **✅ Decided (2026-11-11): A, only OP-3 and OP-4 in this scope.**
>
> - OP-1, OP-2, OP-5 and OP-6 are **out of scope for now**: nothing is built for
>   them (including no `/health` endpoint), and they're handled later as separate
>   work.
> - **Reminder for later, not this scope:** the brief's README must still explain
>   why OP-3/OP-4 were chosen and how the others would be handled. The OP-2 note
>   above is kept as input for that.

---

### Q20. AI-setup artefacts: what gets committed, and where? ✅ _Decided_

**Why it matters:** "Effective and responsible AI use" is 20%. The brief requires the
agent setup to be committed, with personal details removed.

| Option                                                                                                              | Pros                                                                        | Cons                                                                   |
| ------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------- | ---------------------------------------------------------------------- |
| **A. Commit `AGENTS.md`, `prompt-style.md`, and the per-feature `ideal.md` / `plan.md` under `.github/<feature>/`** | Shows the gated workflow (idea, approval, plan, approval, code) as evidence | `prompt-style.md` quotes "the lead"; review for personal details first |
| B. Commit only `AGENTS.md`                                                                                          | Minimal                                                                     | Loses the strongest evidence of process                                |

**Recommendation: A**, after a quick review of `prompt-style.md` for personal
details. Also commit early and often from now on (no commits exist yet), because the
brief asks for unsquashed history.

> **✅ Decided (2026-11-11): A**, so the judges can see the workflow and the
> decisions.
>
> - **Committed:** `AGENTS.md`, `prompt-style.md`, and the per-feature
>   `ideal.md`/`plan.md` under `.github/<feature>/`.
> - **Personal-details check (done 2026-11-11):** `prompt-style.md` contains no
>   names, emails or links. It refers only to "the lead" generically, so it's fine
>   to commit as is.
> - **Commit history:** commit early and often, unsquashed.

---

### Q21. Repository docs in `docs/`: which documents, what format, and when? ✅ _Decided (D open)_

**Why it matters:** the decision to document architecture and patterns in `docs/` is
made (4.1). How it's done still matters:

- **Code quality & design (15%) and the Q&A:** reviewers ask "explain your
  structure". A short architecture doc is the map you present from.
- **Live change (20 minutes):** a "how to add a business rule / endpoint" pattern
  doc tells both you and the AI agent exactly where a change goes, so the agent
  follows the conventions instead of inventing new ones.
- **AI use (20%):** `AGENTS.md` can point the agent at these docs as context, which
  is visible evidence of a thoughtful agent setup.
- **OP-4:** a support team who didn't build the tool also needs to understand its
  shape.
- **Risk:** docs that drift from the code are worse than none, and too many docs is
  its own form of over-engineering.

**Sub-question A: which documents?**

| Option                                                                                                                                                                                                                                                                          | Pros                                                                                                         | Cons                                                                                                                    |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------- |
| A1. One `docs/ARCHITECTURE.md` covering everything                                                                                                                                                                                                                              | One place; least to maintain                                                                                 | Becomes long; patterns and decisions get buried                                                                         |
| **A2. A small set: `docs/ARCHITECTURE.md` (layers, project references, request flow, error contract, cross-cutting concerns) + `docs/PATTERNS.md` (handler-per-use-case, adding a rule/endpoint, idempotency, clock, testing conventions) + the existing `docs/ENTITIES.json`** | Clear split: "what it is" vs "how to extend it"; `PATTERNS.md` doubles as agent guidance for the live change | Two files to keep in sync with the code                                                                                 |
| A3. A2 + ADRs (`docs/adr/0001-*.md`, one per decision)                                                                                                                                                                                                                          | Industry-standard decision log; good audit trail                                                             | Overlaps with `.github/<feature>/ideal.md`, which already records decisions with rejected options; more files for a PoC |

**Sub-question B: diagrams?**

| Option                                                                                                                       | Pros                                                                    | Cons                                        |
| ---------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- | ------------------------------------------- |
| **B1. Mermaid diagrams inside the Markdown (component/layer diagram, one sequence diagram for "add task" with idempotency)** | Rendered by GitHub; text-based, so diffable and AI-editable; no tooling | Limited layout control                      |
| B2. Images (draw.io / PNG)                                                                                                   | Full visual control                                                     | Not diffable; drift silently; extra tooling |
| B3. No diagrams                                                                                                              | Least effort                                                            | Harder to present the structure in the demo |

**Sub-question C: when to write them?**

| Option                                                                                                                                        | Pros                                                                                                        | Cons                                                                                                                       |
| --------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| C1. Now, before any code                                                                                                                      | Docs drive the code                                                                                         | Describes code that doesn't exist; drifts immediately; contradicts `prompt-style.md` ("defer supporting docs until green") |
| **C2. Skeleton when the base is built (after this idea and its plan are approved), then updated in the same commit as each behaviour change** | Docs match real code; small, continuous updates; matches `AGENTS.md` ("update docs when behaviour changes") | Requires discipline on every change                                                                                        |
| C3. At the end, before submission                                                                                                             | One focused pass                                                                                            | Highest drift risk; misses the "visible process in commit history" signal                                                  |

**Sub-question D: the data model file.** `docs/ENTITIES.json` holds DBML, not JSON.
Renaming it to `docs/ENTITIES.dbml` (or embedding it as a Mermaid ER diagram in
`ARCHITECTURE.md`) would let editors and GitHub render it correctly. Renaming needs
your approval, per `AGENTS.md`.

**Recommendation: A2 + B1 + C2.** Use ideal/plan files as the decision log instead of
ADRs, and link to them from `ARCHITECTURE.md`. Add a line to `AGENTS.md` pointing the
agent at `docs/ARCHITECTURE.md` and `docs/PATTERNS.md` before it makes changes. For
D, rename to `.dbml` and keep it as the source of truth for the model; the Mermaid ER
diagram can be added later if it helps the demo.

> **Input from Q6 (2026-11-11):** whatever shape is chosen here, the patterns doc
> must include a **front-end boilerplate** section for the RTK Query pattern (API
> slice, endpoints, tags, idempotency key on mutations, error mapping, when to add a
> Redux slice).

> **✅ Decided (2026-11-11): A2 (with separate back-end and front-end pattern docs) +
> B3 + C2. D is still open.**
>
> - **A2, adjusted:**
>   - `docs/ARCHITECTURE.md`: layers, project references, request flow, error
>     contract, cross-cutting concerns; links to `.github/<feature>/` ideal/plan
>     files as the decision log, with no ADRs.
>   - **Separate pattern docs**, e.g. `docs/patterns/backend.md` and
>     `docs/patterns/frontend.md` (file names confirmed in `plan.md`):
>     - **Back end:** mediator, handler per use case, how to add a rule or endpoint,
>       idempotency, audit, clock, testing conventions.
>     - **Front end:** the RTK Query boilerplate from Q6.
>   - `docs/ENTITIES.json` stays the data model.
>   - `AGENTS.md` points the agent at these docs.
> - **B3:** no diagrams. Structure is described in text and tables; the demo walks
>   through the code instead.
> - **C2:** a skeleton is written when the base is built, then updated in the same
>   commit as each behaviour change.
> - **D (rename `ENTITIES.json` to `.dbml`):** not answered yet; still **open**.

---

## 7. Next step

✅ **This document is green (2026-11-11).** Next, write `plan.md` (same folder):

- carry the decisions above in as **confirmed decisions**, without re-litigating
  them;
- resolve the carried-over items (Q7, Q15(b), Q21 D);
- produce the phased plan.

No code before the plan is approved.
