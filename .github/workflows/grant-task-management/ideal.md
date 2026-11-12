# Grant and task management: idea and decisions

> **Status: GREEN (approved by the developer, 2026-11-12).** Decision record only. No
> implementation steps; the plan is written next
> ([prompt-style.md](../../../prompt-style.md)). **All questions (Q1 to Q17) are decided
> (2026-11-12).** Changes to a decision from here on must be recorded in this file.
> Source requirements: `Software-Developer-Technical-Exercise.pdf` (sections 2, 4, 5, 6).
> Out of scope for this feature: the operations needs OP-1 to OP-6 (separate session).
> **Scope rule (developer):** stick to the exercise scope first. There is **no assigning
> of people** (to grants or tasks) in this feature, but there **is Edit task** (last
> phase; fields: title, priority, due date). Documentation is updated in the same
> commits as the behaviour so it never strays from the repo (see Q4, Q13).

## 1. Context (traced from the code)

- **Done:** mediator with logging, audit and idempotency behaviours; typed errors
  mapped to ProblemDetails with a stable `code`; `IBusinessClock.Today`; identification
  through `X-User-Id`; schema and migration for `Grants`, `Tasks` and the assignment
  tables; front-end shell (login, RTK Query `baseApi`, error and offline handling, theme).
- **Not done:** no grant/task use cases, repositories or controllers; entities have no
  behaviour; no `BR-*` error codes; the main page is empty; no front-end test tooling.
- **Mismatch:** the seed data (5 grants, other names and dates) differs from the exercise
  seed (4 grants). `Tasks.Title` is 200 characters in the schema; BR-3 says 100.
- **Extension beyond the exercise:** the repo models users and assignments
  (`GrantAssignment`, `TaskAssignment`); the architecture reserves `ASSIGNEE_NOT_ON_GRANT`.
- **Front-end setup (traced):**
  - `App.tsx` switches `MainPage` / `LoginPage` on the signed-in user, with no router
    (setup plan OQ-F4: add `react-router` only if screens later need URLs). Signing out
    unmounts `MainPage`, so component state cannot leak between officers.
  - `baseApi` adds `X-User-Id`, retries queries (network, timeout, 5xx; max 3) and never
    retries mutations. `tagTypes` is empty, `MainPage` renders only a hidden heading.
  - The error listener deliberately ignores 400, 404 and 409 (shown inline next to the
    action), routes unreachable errors to the banner and everything else to a toast
    named through `errorMessages.ts`. `toApiError` supplies `code`, `traceId`, `fieldErrors`.
  - Theme classes exist for buttons, fields, alerts and tags (`tag-overdue`, `tag-high`,
    `tag-medium`, `tag-open`, `tag-closed`, `tag-completed`). There are **no** table,
    dialog, select or filter-bar styles; `.input.select` exists only in `style-guide.html`.
  - UI-STYLE rules that bind this feature: yellow is for overdue and focus only, red is
    for errors and High priority (never overdue), colour is never the only signal, one
    primary button per view (Add task), row actions are secondary, works from 320px wide,
    refused business actions appear inline at the action, success toasts only when the
    result is not already visible.
  - No front-end test tooling; the setup plan anticipated Vitest + React Testing Library.

## 2. The ideal

A grants officer opens one page, finds a grant, works its tasks (add, complete, filter)
and closes the grant, with every business rule BR-1 to BR-6 enforced by the server. The
screen only reflects and explains those rules; it never replaces them. Editing a task is
a later, cuttable extra (Q4).

## 3. Screen decisions

**Grants list (main page)**
- Table columns: reference number, organisation, application date, status,
  "completed / total tasks", actions.
- Filter: status only (All, Open, Closed), default Open.
- Order: Open first, then application date, oldest first.
- Actions: **Tasks** (opens the grant's tasks) and **Close**.
- **Close** is disabled when the grant is already closed or has a pending High-priority
  task (BR-5), with the reason shown. A refusal from the API is shown beside the button.

**Grant tasks view**
- Reached from **Tasks** by a list/detail swap inside the main page, with a Back action.
  No router; the grants filter is kept when returning.
- Header buttons: **Close grant** (same behaviour as in the list) and **Add task**.
- Tasks table: title, priority, due date, status, actions. (The requested "Assigned"
  column is deferred with assignment, Q2/Q12.)
- Priority badge: High red (`tag-high`), Medium (`tag-medium`), Low neutral (plain
  `.tag`); the text label is always shown.
- Due date shown as `dd-MMM-yyyy`, formatted from the `yyyy-MM-dd` string without a
  JavaScript `Date` (avoids timezone shifts). A pending overdue task also shows the
  overdue badge: the existing yellow `tag-overdue`, worded **"Overdue by N days"** as in
  UI-STYLE (open question Q3: the requested wording was "Late").
  Overdue and `daysOverdue` come from the API (business clock), never from the browser
  clock (BR-6). Completed tasks never show it.
- Filters: status (default Pending), priority, title search. Order: Pending first, then
  priority High to Low, then earliest due date. Empty states: "No task" when the grant
  has none, "No task matches the filters" otherwise.
- Actions per task: **Complete** (pending only) and, in the last phase, **Edit**
  (pending only). On a closed grant the grant is read-only (Q5): Add task, Complete
  and Edit are disabled with the reason "This grant is closed."
- **Add task** dialog: title (max 100), priority, due date. (**Edit task** reuses it in
  the last phase.) Field errors appear beside the field; API refusals (BR-1, BR-2)
  appear in the dialog.

**Front-end foundations decided for these pages**

| Topic | Decision | Why / rejected alternative |
| --- | --- | --- |
| Navigation | `selectedGrantId` in `MainPage` component state; list/detail swap | Matches no-router decision OQ-F4; `react-router` rejected as a new dependency for two views |
| Focus | On swap, focus moves to the grant heading (the hidden `h1` becomes the grant's title); on Back, focus returns to that grant's **Tasks** button | Without a router nothing else tells keyboard and screen-reader users the view changed |
| Dialogs | Native `<dialog>` with `showModal()` for Add task (and Edit task later) | Traps focus and closes on Escape with no dependency |
| Narrow screens | Real `<table>` in a horizontal scroll wrapper (or stacked, labelled rows at narrow widths) | A 6-column table breaks the 320px floor otherwise |
| New styles | Add table, dialog, select and filter-bar classes to `theme.css` using existing tokens; show them in `style-guide.html`; update `UI-STYLE.md` | UI-STYLE section 11 process; docs changes approved by the developer (Q4) |
| Date picker | The grant detail response returns the server's business "today"; the picker's minimum is the later of that and the application date. Client validation is a hint; the server decides | Browser clock may differ from the `Asia/Singapore` business timezone |
| Idempotency | Add task and Edit task send an `Idempotency-Key` created once per user intent and kept until the form changes or succeeds | `frontend.md` pattern; never generated in `prepareHeaders` |
| Pending state | Each row action and dialog submit is disabled with a words-based pending label while its request runs | UI-STYLE section 6 |
| Messages | 400 beside the field; 404/409 as an inline alert at the action (e.g. Close refused by BR-5); success toast only where the result is not visible; new endpoints get action names in `errorMessages.ts` | UI-STYLE section 7 |
| Offline banner | Reword to "Can't reach the server. Check your connection, then try again." | Current text says "select Sign in again", wrong for a signed-in officer |
| API data | Tags for grants and tasks added to `tagTypes`; mutations invalidate the affected grant, task and list tags | `frontend.md` pattern |
| Tests | Vitest + React Testing Library + jsdom, for component tests | New dependencies: **ask the developer before adding** |

## 4. Business-rule decisions

**Delivery order (Q4, Q12):** phase 1 is the exercise scope: grants list with filter and
counts, grant tasks view, Add task (title, due date, priority), Complete, Close,
overdue endpoint, BR-1 to BR-6, migration to a 100-character title, seed from the PDF,
tests, and the matching documentation. Phase 2 (extra, cuttable): Edit task. Assigning
officers (Assign, Assigned column, assignee picker, `GET /users`) is **not** in this
feature.

| Topic | Decision | Rejected alternative |
| --- | --- | --- |
| BR-4 repeat complete | `200` with the existing task, completion date unchanged, audited as `NoChange`. A retry is harmless, even if the grant has since been closed | `409` (punishes a retry after a lost response) |
| BR-5 close rule | Only a pending **High** task blocks closing (as written). Closing a closed grant returns `200` unchanged (`NoChange`) | "Any pending task blocks" (contradicts the exercise) |
| Check order, add task | 404 grant, then 400 input (BR-3, BR-2, all errors together), then 409 closed grant (BR-1) | State check before input check |
| BR-2 | Due date >= today (business clock) and >= grant application date | |
| Closed grant | **Read-only (Q5):** Add task is refused with `BR-1`; completing a still-pending task or editing a task is refused with `409 GRANT_CLOSED`. Completing an already-completed task stays a `200` no-op | Only Add task blocked |
| Edit task (phase 2) | Fields: **title, priority, due date** only. Pending tasks on open grants only. BR-3 always applies; due date is checked (BR-2) only when it changes, so an unchanged late date can be saved. Status, grant and completed tasks are not editable; there are no assignees to edit | Editing completed tasks |
| Assignment | **Out of scope (Q2, Q12).** No assign, no unassign, no grant team. `GrantAssignment` and `TaskAssignment` stay in the schema, unused (Q16) | Grant team (needs team management), any officer (needs a users endpoint and screen) |
| Idempotency | Add task and Edit task are idempotent commands (key from the form intent, Q14); Complete and Close are safe by state | Key on every write |
| Rule location | Plain C# in Core (grant and task methods, `today` passed in), unit-tested without EF | Rules inside controllers or handlers only |
| Error codes | `BR-1`, `BR-2`, `BR-3`, `BR-5`, `GRANT_CLOSED`, plus (phase 2) a code for editing a completed task, `VERSION_CONFLICT` (412) and `PRECONDITION_REQUIRED` (428) | |
| Concurrent edits | Optimistic concurrency on tasks: `Version` column, `If-Match` on Edit, `412` on a stale version (Q17) | Last write wins; leaving it to OP-2 |
| Filtering and sorting of tasks | In the browser; a grant's task list is small | Server-side query parameters |
| What the API must return for the screen | Grants list: total, completed and pending-High task counts. Grant detail: tasks with `isOverdue`, `daysOverdue` and the server's "today". The overdue and Close-disabled states are decided by the server | Computing these from the browser clock or by fetching every task |
| Title length | A new migration changes `Tasks.Title` to a 100-character maximum (Q9) and adds `Tasks.Version` (Q17); the limit is also enforced in Core | Keep 200 |
| Seed | Replaced to match the exercise (PDF section 6): 4 grants and their tasks, relative to today; the 5 demo officers stay for sign-in; no assignment rows (Q10) | Keep the 5-grant seed |
| Business time | `Asia/Singapore` is kept and committed (Q11) | `UTC` |

## 5. Request shape decisions

The exercise lets us choose routes, methods and shapes. Decided (approved by the
developer): **inputs that identify a resource go in the path, optional list filters in
the query string, and write payloads in the JSON body. No request body on `GET`.**

| Endpoint | Shape | Notes |
| --- | --- | --- |
| `GET /grants?status=Open\|Closed` | Optional `status`; omitted means all. The screen sends `Open` by default | Returns total, completed and pending-High counts per grant (the exercise asks for pending; the screen needs completed / total) |
| `GET /grants/{id}` | Path id | Grant, its tasks (with `isOverdue`, `daysOverdue`) and the server's "today" |
| `POST /grants/{id}/tasks` | Body: title, due date, priority | Idempotent command (`Idempotency-Key` header); `201` with the task |
| `PUT /tasks/{id}` | Headers `If-Match: "<version>"` and `Idempotency-Key` (Q14); body: title, due date, priority | Phase 2 (edit); `428` without `If-Match`, `412` on a stale version (Q17); a retry with the same key replays the first result |
| `PUT /tasks/{id}/complete` | No body | `200` with the task; repeat is a no-op |
| `PUT /grants/{id}/close` | No body | `200` with the grant; repeat is a no-op |
| `GET /tasks/overdue` | None | Most overdue first; includes grant reference and organisation |

Conventions: dates are `yyyy-MM-dd` strings, moments are UTC ISO-8601, enums are
strings, errors use the existing ProblemDetails contract.

| Rejected | Why |
| --- | --- |
| Filters in a `GET` body | Browser `fetch` (so RTK Query) cannot send a body on `GET`; proxies and OpenAPI 3.0 may drop or reject it; the body is not in request logs (OP-4) |
| `POST /grants/search` for filtering | A read modelled as a write, not cacheable, differs from the exercise's `GET /grants`; worth revisiting only if many list filters appear |
| HTTP `QUERY` method | Still a draft standard; no browser, CORS or ASP.NET default support |
| Server-side task filter or search | The tasks of one grant are already returned by `GET /grants/{id}` and the list is small |

## 6. Testing intent

Core unit tests for every rule with boundary dates and a fake `today`; Data tests for
the pending-count, overdue and detail queries, the seed and the migration; API tests for
status codes, error codes and body shape; front-end component tests for form validation,
API error display, filters, overdue badge, Close-disabled state and the read-only
closed grant. Time zones (Q11): a Core test with a business zone whose date differs
from UTC, an API test showing the server's "today" in the grant detail, and a manual
check with the browser in another zone. Concurrency (Q17): a Core test that a stale
version is refused, a Data test that EF's concurrency token catches a race between two
contexts, an API test for `428` and `412` and for the new `ETag`, and a front-end test
that the Edit dialog shows the conflict message. Seed aligned with exercise section 6 so the
overdue logic is always demonstrable.

## 7. Known limitations

- No deep links or browser Back inside the app (no router); refresh returns to the list.
- `GET /tasks/overdue` is built and tested but has no screen in this feature.
- The `Tasks.Title` migration is applied automatically at start-up (existing behaviour of
  `DatabaseInitializer`), but the seed runs only on an empty database, so a developer
  with an existing database file must delete it (README "Reset the demo")
  to see the new seed.
- Pending Medium or Low tasks can exist when a grant is closed (BR-5 only blocks High)
  and then cannot be completed (read-only grant); see Q15.
- The grants list's Close-disabled state can be stale if another officer adds a High
  task after the list loaded; the server still refuses (BR-5) and the screen shows why.
- No authorisation: any identified officer can act on any grant.
- Native `<dialog>` and focus handling must be checked by hand in a browser as well as
  by component tests.
- Concurrent edits to the same task are refused with `412` (Q17); other races (for
  example closing a grant while another officer adds a High task, BR-5) are not guarded
  beyond the database transaction and belong to OP-2 (another session).

## 8. Open Questions

> Each question lists what it is, why it matters, how it affects the work, options with
> pros and cons, and a **recommendation**. Questions marked **Decided** are closed;
> the ideal above assumes the recommendation for every other one until answered.

### Q1. Which tasks block closing a grant? — **Decided: A**

- **What:** the requested screen disables Close "if all tasks are not finished". BR-5
  in the exercise says only a pending **High-priority** task blocks closing.
- **Why it matters:** BR-5 is a graded business rule and is tested by the reviewers
  (and possibly changed in the live session). Behaviour that differs from the PDF reads
  as a bug.
- **Affects:** the Core close rule, its tests, the grants-list counts the API returns,
  the Close button state and the seed scenario (GR-100001 has a pending High task, so
  it must be refused).

| Option | Pros | Cons |
| --- | --- | --- |
| A. BR-5 as written (pending High blocks) | Matches the exercise; Medium/Low work can remain when closing | Differs from your stated screen behaviour |
| B. Any pending task blocks | Simple, matches your wording | Contradicts BR-5; closing GR-100002 would be blocked by a Low task |
| C. A with a warning in the dialog when other tasks are still pending | Rule stays correct; officer is told | One more dialog to build |

**Recommendation: A**, and add C later if wanted. The server enforces it; the screen
only mirrors it and shows the reason.

### Q2. Who can be assigned to a task? — **Decided: anyone, but assignment is out of scope for now (developer, 2026-11-12; see Q12)**

> Reason given: follow the exercise scope first, so no grant team and no assignment.
> Consequences: `GrantAssignment` and `TaskAssignment` stay in the schema, unused (Q16);
> `ASSIGNEE_NOT_ON_GRANT` is not implemented; no `GET /users`; `ARCHITECTURE.md` and
> `ENTITIES.json` are updated to say the assignment tables are not used by this feature.
> If assignment returns later, "any officer" is the agreed rule.

- **What:** the exercise has no users or assignment at all. This repo already models
  grant teams (`GrantAssignment`) and task assignees, and its architecture says a task's
  assignee must belong to the grant (`409 ASSIGNEE_NOT_ON_GRANT`). There is no screen
  or endpoint to manage the grant team.
- **Why it matters:** it decides who appears in the Assign and Add-task pickers, and
  whether the grant team is read-only seed data.
- **Affects:** grant-detail response (needs the team), one extra rule and error code,
  seed data (every grant needs a team), and tests for the rule.

| Option | Pros | Cons |
| --- | --- | --- |
| A. Grant team only, team is seed data | Keeps the existing architecture and rule; realistic | Team cannot change from the UI; new grants would have no team |
| B. Any officer | Simplest picker; no team needed | Needs approved edits to `ARCHITECTURE.md`/`ENTITIES.json`; drops a documented rule |
| C. A plus a "manage team" action | Complete | Scope creep far beyond the exercise |

**Recommendation: A.** Document the team as seed-only in the README.

### Q3. Overdue badge wording and colour — **Decided: A**

- **What:** you asked for a bright "Late" badge. UI-STYLE reserves yellow for overdue,
  forbids red for overdue, and the sketch uses "Overdue by 10 days".
- **Why it matters:** overdue visibility is the point of the exercise (BR-6), and the
  wording must be consistent between the badge, tests and README.
- **Affects:** one badge component, the API (`daysOverdue`), front-end tests.

| Option | Pros | Cons |
| --- | --- | --- |
| A. "Overdue by N days", yellow `tag-overdue` | Matches UI-STYLE; more information | Longer than "Late"; needs `daysOverdue` |
| B. "Late", yellow | Short, as you asked | Less informative |
| C. "Late" in red | Bright | Breaks the colour rules; clashes with High priority red |

**Recommendation: A**; B is a one-word change if you prefer it. Avoid C.

### Q4. Can completed tasks be edited, and what is validated on edit? — **Decided: A; Edit is the last phase; docs are updated with the code (developer, 2026-11-12)**

> Decision: the exercise scope comes first. Edit (an addition beyond the exercise, which
> has no editing or assignment) is a separate final phase and can be cut without
> affecting the core; assignment is out of scope (Q12). Edit rule: pending tasks only;
> BR-3 always; due date checked only when changed. Add task matches the exercise
> exactly (title, due date, priority). The developer also wants the documentation
> updated so it does not stray from the repo: `ARCHITECTURE.md`, `ENTITIES.json`,
> `UI-STYLE.md`, `theme.css`/`style-guide.html`, the pattern files and the README are
> changed in the same commits as the behaviour (Q13).

- **What:** BR-2 is written for a **new** task. Editing is your addition. Re-saving a
  task that is already late must not fail just because its due date is in the past.
- **Why it matters:** without a rule, an officer could not rename an overdue task, or
  could rewrite history of a completed one.
- **Affects:** the edit use case, a new error code, the Edit button visibility, tests.

| Option | Pros | Cons |
| --- | --- | --- |
| A. Pending tasks only; BR-3 always; due date checked only when changed | Safe; late tasks stay editable | Completed tasks cannot be corrected |
| B. Any task; same checks | Flexible | Lets history change after completion (BR-4 intent) |
| C. Re-apply BR-2 on every edit | Simplest to explain | Cannot rename an overdue task |

**Recommendation: A.**

### Q5. What can still be done on a closed grant? — **Decided: B, the grant becomes read-only (developer, 2026-11-12)**

> Consequences: Add task is refused with `BR-1`; completing a pending task (and editing,
> in the last phase) is refused with `409 GRANT_CLOSED`; the screen disables those
> actions with the reason. Completing an already-completed task stays a `200` no-op so
> retries are safe. See Q15 for the side effect on pending Medium/Low tasks.

- **What:** BR-1 only blocks **adding** tasks to a closed grant.
- **Why it matters:** inventing more restrictions adds rules nobody asked for;
  allowing everything lets a closed grant keep changing.
- **Affects:** Core rules, which buttons are enabled on a closed grant, tests.

| Option | Pros | Cons |
| --- | --- | --- |
| A. Only Add task blocked | Exactly the exercise; no invented rules | A closed grant can still change in other ways |
| B. Whole grant read-only | Consistent "closed" meaning | New rule and error codes; Complete on a closed grant is blocked |

**Recommendation: A**, and note B as a follow-up.

### Q6. Response to completing an already-completed task (BR-4) and closing a closed grant — **Decided: A**

- **What:** the exercise asks us to decide and explain.
- **Why it matters:** it is explicitly assessed, and it decides how retries over an
  unreliable connection behave.
- **Affects:** handlers, audit outcome, API tests, README explanation.

| Option | Pros | Cons |
| --- | --- | --- |
| A. `200` with the unchanged task, audited `NoChange` | Idempotent; a retry after a lost response is harmless; the screen needs no special case | Caller cannot tell a repeat from a first success from the status alone |
| B. `409 Conflict` | Explicit | Retries look like failures; the screen must treat it as success |
| C. `204 No Content` | Light | Loses the current task state |

**Recommendation: A**, applied the same way to closing a closed grant.

### Q7. Order of checks when adding a task — **Decided: A**

- **What:** one request can break several rules (closed grant and a bad date).
- **Why it matters:** the error the officer sees and the one a test expects.
- **Affects:** add-task handler, API tests, the dialog message.

| Option | Pros | Cons |
| --- | --- | --- |
| A. 404, then input (BR-3, BR-2), then state (BR-1) | Input errors are cheap and shown first; standard validation-before-state | A closed grant with bad input reports the input first |
| B. 404, then state (BR-1), then input | Tells the officer the grant is closed immediately | Fixing the input then hits a second error |

**Recommendation: A.** Return all input errors together.

### Q8. Navigation without a router — **Decided: A, component state swap for now**

- **What:** the tasks view is a list/detail swap in `MainPage` state.
- **Why it matters:** no URL per grant, no browser Back, refresh returns to the list.
- **Affects:** `MainPage` structure, focus handling, and whether a dependency is added.

| Option | Pros | Cons |
| --- | --- | --- |
| A. Component state swap | Matches the setup plan; no dependency; small | No deep links or browser Back |
| B. `react-router` | Real URLs, Back works | New dependency; undoes decision OQ-F4; more setup for two views |

**Recommendation: A.**

### Q9. Title length in the database (100 vs 200) — **Decided: B, migrate to 100; the migration runs automatically (developer, 2026-11-12)**

> Consequences: a new EF migration sets `Tasks.Title` to 100 characters (model,
> snapshot, `ENTITIES.json`). Applying it needs no new code: `DatabaseInitializer`
> already calls `MigrateAsync` on every start-up, so an existing database is upgraded
> when the API starts. SQLite does not enforce text length from the column type, so the
> same migration should also add a check constraint (`length("Title") <= 100`), like the
> existing status check constraints, so the database enforces it too (my addition;
> object if unwanted). A Data test confirms the migration applies to a database created
> by the initial migration and that a 101-character title is rejected.

- **What:** BR-3 allows 100; the schema column and `ENTITIES.json` say 200.
- **Why it matters:** the rule is enforced in Core either way, but the schema should not
  contradict it, and a migration is a change to a committed migration history.
- **Affects:** one migration, the seed database reset, `ENTITIES.json`.

| Option | Pros | Cons |
| --- | --- | --- |
| A. Keep 200, enforce 100 in Core | No migration; rule stays in business logic | Schema looser than the rule |
| B. New migration to 100 | DB mirrors the rule | Migration plus docs change; SQLite rebuilds the table |

**Recommendation: A.**

### Q10. Seed data: replace with the exercise data? — **Decided: A, replace to match the PDF (developer, 2026-11-12)**

> Consequences: exactly the 4 grants and 6 tasks of PDF section 6, dates relative to
> today. The 5 demo officers stay (sign-in needs them). No assignment rows, since
> assignment is out of scope (Q12). The seed test is rewritten. Existing databases keep
> the old data until deleted (see section 7).

- **What:** the repo seeds 5 grants with other names and dates. The exercise defines 4
  grants and exact due dates so overdue can always be demonstrated. The seed runs only
  on an empty database and its test pins the old values.
- **Why it matters:** reviewers compare the screen against section 6; BR-5 and BR-6
  demos depend on it.
- **Affects:** `SeedData`, the seed test, user/team/assignee seed rows, and anyone with an
  existing `grant-tracker.db` (must delete it and its sidecars, as the README says).

| Option | Pros | Cons |
| --- | --- | --- |
| A. Replace with the exercise's 4 grants (keep users; no assignment rows) | Matches the exercise; predictable demo | Changes existing tests and seed |
| B. Keep the 5-grant seed | No change | Does not match the exercise; the overdue demo differs |
| C. Exercise grants plus extra demo grants | Richer demo | Extra data to explain and test |

**Recommendation: A.** The Assigned column is out of scope (Q12), so no assignees are seeded.

### Q11. Business timezone — **Decided: A, keep `Asia/Singapore` and commit it (developer, 2026-11-12)**

> Purpose stated by the developer: check what happens when the server and the client
> are in one time zone but the clock is in another. This is why "today" always comes
> from the server's business clock, never the browser (see the date-picker row in
> section 3). Test intent is in section 6.

- **What:** `appsettings.json` currently has an **uncommitted** change from `UTC` to
  `Asia/Singapore`.
- **Why it matters:** "today" decides BR-2 and BR-6, and the seed dates. Around midnight
  UTC the overdue state differs between zones.
- **Affects:** tests (must always use a fake clock), the README, and what the date picker
  minimum shows.

| Option | Pros | Cons |
| --- | --- | --- |
| A. Keep `Asia/Singapore` and commit it | Matches the developer's location | Reviewers in another zone see different "today" |
| B. Revert to `UTC` | Neutral, reproducible | Differs from your local day near midnight |

**Recommendation: A** if intentional, with the zone stated in the README. Otherwise B.

### Q12. Can an assignment be removed? — **Decided: assignment is out of scope entirely (developer, 2026-11-12)**

> No assign to grants or tasks, so no unassign either. Revisit with Q2 if assignment
> returns.

- **What:** you listed Assign but not Unassign.
- **Why it matters:** a wrong assignee cannot be corrected.
- **Affects:** one more endpoint, button and tests.

| Option | Pros | Cons |
| --- | --- | --- |
| A. No unassign | Matches the request; smaller | Mistakes are permanent |
| B. Add Unassign | Complete | More surface to build and test |

**Recommendation: A for now**, listed as a follow-up.

### Q13. Documentation and dependency approvals — **Decided: approve both (developer, 2026-11-12)**

> Approved: doc changes, in the same commits as the behaviour, and the new front-end
> test dependencies (Vitest, React Testing Library, jsdom). The developer also asks that
> the **back-end pattern doc be made precise and detailed, with example code** from tests
> to API to Core services, as the boilerplate future changes follow:
> - `docs/patterns/backend.md` gets worked examples for each recipe: a rule on the
>   entity (plain C#, `today` passed in); a use-case folder (query or command plus
>   handler, audit target and outcome); a repository port and its EF implementation; a
>   controller action and request DTO; DI registration; a new error code and its HTTP
>   mapping; an idempotent command; and one test at each level (Core with Moq and a fake
>   clock, Data with SQLite in-memory, Api with `WebApplicationFactory`).
> - The examples are copied from the real implementation after it compiles and passes
>   (each cites its source file), never written from memory, so the doc cannot stray
>   from the code. The plan includes a step to re-check them against the files before
>   the feature is finished.
> - Front-end examples follow the same rule in `frontend.md`.

- **What:** `AGENTS.md` says do not change `docs/` without approval and ask before adding
  dependencies. This feature needs: `UI-STYLE.md`, `theme.css`, `style-guide.html`,
  `ARCHITECTURE.md` and `ENTITIES.json` updates, `backend.md`/`frontend.md` additions, and
  Vitest, React Testing Library and jsdom.
- **Why it matters:** the docs say they must change in the same commit as behaviour, but
  the working agreement forbids changing them unasked.
- **Affects:** the plan's phases and the front-end test level.

| Option | Pros | Cons |
| --- | --- | --- |
| A. Approve both now, in the plan | One clear scope; docs stay accurate | You approve before seeing the details |
| B. Approve per phase | Full control | Slows the work |
| C. No front-end tests, no docs changes | Fastest | Docs drift; fails the exercise's multi-level testing intent |

**Recommendation: A**, with the plan listing each doc and dependency explicitly.

### Q14. Which writes carry an idempotency key — **Decided: B (developer, 2026-11-12)**

> Add task and Edit task carry an `Idempotency-Key` (one key per user intent, kept until
> the form changes or succeeds). Complete and Close carry none: a repeat is a harmless
> no-op. The Edit retry replays the first result before the version check runs, so a
> lost-response retry never shows a false `412`. Docs and tests cover both commands.

- **What:** when are retries made safe by an `Idempotency-Key`, and when by the HTTP
  method itself? Today only Add task (`POST`) carries a key.
- **How the industry does it:**
  - **HTTP itself** (RFC 9110): `GET`, `HEAD`, `OPTIONS`, `PUT` and `DELETE` are
    idempotent by definition, so repeating them has the same effect as sending them once.
    `POST` and `PATCH` are not, so they are the methods that need help.
  - **A client-generated key on `POST`**, stored by the server with the response:
    Stripe, Adyen, Twilio (`I-Twilio-Idempotency-Token`), PayPal (`PayPal-Request-Id`)
    and Square (`idempotency_key`) all do this; AWS "client tokens" and Google API
    request ids are the same idea. Typical rules: the key is unique per request intent,
    kept about 24 hours, a retry replays the stored response (not a re-run), the same key
    with a different payload is an error, and a retry while the first request is still in
    flight is rejected or waits.
  - **The IETF draft** `draft-ietf-httpapi-idempotency-key-header` (version 08, still a
    draft, expires April 2027) standardises that header: missing key `400`; same key with a
    different payload `422`; same key while the first is still in flight `409`.
  - **Our implementation already follows that shape:** key required on opted-in commands,
    SHA-256 of the request, replay of the stored result, `422 IDEMPOTENCY_KEY_MISMATCH`,
    unique-key resolution for concurrent retries. Gaps: no 24-hour cleanup yet (README
    follow-up) and a concurrent duplicate is resolved by replay rather than `409`.
- **The catch created by Q17:** a `PUT` is safe to repeat only while nothing else
  changed. With versions, an Edit whose response was lost has already bumped the version,
  so its **retry is refused with `412`** even though the edit succeeded. The officer
  would see a conflict that is really their own change. Complete and close do not have
  this problem (a repeat is a harmless no-op with no `If-Match`).
- **Why it matters:** unreliable connections (OP-3, another session, but the design must
  not contradict it) and a false "someone else changed this" message would erode trust.
- **Affects:** whether `PUT /tasks/{id}` requires an `Idempotency-Key` too, the Edit
  dialog (one key per intent, per `frontend.md`), the idempotency tests and the README.

| Option | Pros | Cons |
| --- | --- | --- |
| A. Key on Add task only; Edit relies on `If-Match` | Smallest | A lost-response Edit retry shows a false conflict |
| B. Key on Add task **and** Edit; Complete and Close rely on method and state | Retry of Edit replays the original `200` (the idempotency behaviour runs before the version check); matches industry (keys where the effect is not naturally repeatable) | One more header on Edit; one more stored record per edit |
| C. Key on every write | Uniform | Needless for Complete and Close; more storage and front-end state |
| D. Edit retry compares the new state with the request and treats "already applied" as success | No key needed | Fragile (two officers making the same change look identical); more custom logic |

**Recommendation: B.** It is the industry norm (keys where a repeat is not naturally
harmless), it reuses existing behaviour, and it removes the false-conflict case. Edit is
phase 2, so this only affects that phase.

### Q17. Two officers edit the same record at once — **Decided: B, optimistic concurrency (developer, 2026-11-12)**

> **Decision and consequences**
> - `Tasks` gets an integer `Version`, set to 1 on creation and incremented by the entity
>   on **every** change (complete and edit). It is an EF Core concurrency token. It is
>   added in the same new migration as the 100-character title (Q9), so the column
>   exists from phase 1 even though only Edit (phase 2) enforces it. Grants get no
>   version: nothing edits their fields; close is safe by state.
> - Reads return `version` in each task's body (grant detail, overdue list, add and
>   complete responses). The single-task responses also send it as an `ETag`.
> - Edit (`PUT /tasks/{id}`) **requires** `If-Match: "<version>"`. Missing header:
>   `428 PRECONDITION_REQUIRED`. Stale version: `412 VERSION_CONFLICT`. The handler
>   compares versions explicitly (clean error) and EF's concurrency token catches a race
>   between load and save (`DbUpdateConcurrencyException`, translated in `EfUnitOfWork`
>   to a Core `AppException` so the audit behaviour records it as `Rejected`).
> - **Complete and close do not require `If-Match`**, so a retry stays harmless (Q6).
>   Because they bump the version, an editor holding an older version is refused.
> - Front end: the Edit dialog sends the version it loaded. A `412` is shown inside the
>   dialog ("Someone else changed this task. Close this dialog to see their change, then
>   edit again"); the listener must ignore 412 and 428 like 400/404/409, and the failed
>   mutation invalidates the task's tags so the list refreshes.
> - Docs to update with the code: error table (412, 428), CORS (allow `If-Match`,
>   expose `ETag`), `ENTITIES.json` (`Version`), `backend.md` example.
> - Because Edit is a cuttable extra, if Edit is cut the column and the codes still
>   ship but nothing enforces them; the README says so.
> - This is the mechanism OP-2 (another session) would extend to other writes.

*Original analysis, kept for the record:*

- **What:** officer A changes a due date to 30 November 2026, and a little later officer
  B, who loaded the old value, changes it to 2 December 2026. Without a rule the second
  save silently overwrites the first ("last write wins"); A's change is lost and neither
  officer is told. (The exercise has no grant-date edit; the real equivalent here is
  Edit task's due date and title, in the last phase.)
- **The standard:** **optimistic concurrency control**. Each record carries a version.
  The client sends the version it read. The server saves only if the stored version
  still matches; otherwise it refuses. Over HTTP the standard form is an `ETag` on the
  response and an `If-Match` header on the update, with `412 Precondition Failed` when it
  does not match (and `428 Precondition Required` when the header is missing). In EF
  Core it is a concurrency token; a clash raises `DbUpdateConcurrencyException`. SQLite
  has no automatic row-version type, so the version would be an integer column the code
  increments on every change. Pessimistic locking (locking the record while someone
  edits) is the other approach, rejected for web apps: locks are left behind by closed
  tabs and lost connections.
- **Walk-through:** both load the task at version 3. A saves, so the task is version 4.
  B sends version 3 and gets `412`; the dialog says "Someone else changed this task.
  Reload to see their change", then B decides whether to repeat the edit.
- **Why it matters:** silent lost updates are the usual real-world data bug, and the
  exercise's OP-2 (many officers per grant) is about exactly this. Idempotency keys do
  not help: they detect a retry of the *same* request, not two different edits.
- **Affects:** one column and migration, `ETag`/`If-Match` handling in the controller,
  a new error code, a front-end "changed by someone else" message, tests at every level.
  Status changes (complete, close) need no version: they are safe by state.

| Option | Pros | Cons |
| --- | --- | --- |
| A. Last write wins (today's behaviour) | Nothing to build | Silent lost updates |
| B. Optimistic concurrency (version column, `ETag`/`If-Match`, `412`) | Standard; no locks; officers are told | More code, a migration, front-end handling |
| C. Leave it for the OP-2 session, and add B when Edit is built | Keeps this feature in scope; Edit is itself a cuttable extra, so phase 1 has no edit to conflict | The limitation exists until then |

**Recommendation was C; the developer chose B.**

### Q15. Pending Medium/Low tasks on a closed grant, and the overdue list — **Decided: A (developer, 2026-11-12)**

> Follow BR-6 literally: tasks of closed grants that are pending and past due stay on
> `GET /tasks/overdue`, each row shows its grant's status, and `isOverdue` stays true.
> The README states this as an assumption; excluding them is a possible follow-up.

- **What:** BR-5 only blocks closing when a pending **High** task exists (Q1), so a
  grant can close with pending Medium or Low tasks. With a read-only closed grant
  (Q5) those tasks can never be completed. BR-6 says a task is overdue when it is not
  completed and past its due date, and `GET /tasks/overdue` lists overdue tasks across
  **all** grants. Those frozen tasks would stay on the overdue list for ever.
- **Why it matters:** the overdue list is a main deliverable and a likely live-change
  topic. Officers would see overdue work they cannot act on.
- **Affects:** the overdue query, its tests, the overdue endpoint's response (grant
  status), and how the README explains BR-6.

| Option | Pros | Cons |
| --- | --- | --- |
| A. Follow BR-6 literally: include them, and show the grant's status in each row | Exactly the PDF wording ("across all grants"); simple, predictable tests | Officers see work that cannot be completed |
| B. Exclude tasks of closed grants from the overdue list | The list holds only actionable work | Departs from the PDF's literal rule; "overdue" and `isOverdue` could disagree |
| C. A, and on closing a grant refuse while any task is pending | No frozen tasks at all | Contradicts BR-5 (only High blocks) and Q1 |

**Recommendation: A**, documented in the README as an assumption, with B as a possible
follow-up. Note `isOverdue` on a closed grant's pending task stays true.

### Q16. Keep or remove the unused assignment tables — **Decided: A, keep and explain (developer, 2026-11-12)**

> The assignment entities, configurations and tables stay. A short code comment on
> `GrantAssignment`, `TaskAssignment` and their configurations says they are present
> but unused by the grant and task features (assignment is out of scope), and
> `ARCHITECTURE.md` and `ENTITIES.json` say the same.

- **What:** with assignment out of scope (Q2, Q12), `GrantAssignment`,
  `TaskAssignment`, their tables, their navigation properties on `Grant` and
  `GrantTask`, and the docs describing them are unused by this feature.
- **Why it matters:** the docs must match the repo (Q4). Unused schema is noise, but
  removing it deletes work the developer already approved and needs another migration.
- **Affects:** the Q9 migration, `ENTITIES.json`, `ARCHITECTURE.md` section 8, the seed.

| Option | Pros | Cons |
| --- | --- | --- |
| A. Keep, mark as "present, not used by the grant and task features" in the docs | No schema churn; ready if assignment returns | Dead code and tables in the repo |
| B. Remove them in the same migration as Q9 | Schema matches the exercise exactly | Throws away existing work; needs a larger migration and test changes |

**Recommendation: A.**
