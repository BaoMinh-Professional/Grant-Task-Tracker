# Setup repo: front-end base, implementation plan

> **Stage:** 2, implementation plan. **Status:** ✅ **Approved by the developer on
> 2026-11-12.** Implementation follows Phases 1–7. The agent makes no commits or pull
> requests.
> **Source of decisions:** [ideal.md](./ideal.md) (✅ green, 2026-11-11), carried in
> as-is. Front-end decisions made in discussion on 2026-11-11 and 2026-11-12 are
> recorded in section 3.2.
> **Companions:** [setup-be-plan.md](./setup-be-plan.md) (the back end this front end
> talks to), [docs/UI-STYLE.md](../../../docs/UI-STYLE.md) (the visual style), and
> the design references [style-guide.html](./style-guide.html),
> [login.html](./login.html) and [theme.css](./theme.css).
> **Open questions:** OQ-F1 to OQ-F7 decided 2026-11-12 (section 12; index in
> section 11).

---

## 1. Goal and scope

### 1.1 Goal

Create a **front-end base** in `frontend/` that builds, lints and runs against the
existing API. It has every cross-cutting piece the grant and task screens will need
(theme, store, API client, error handling, toasts, offline banner, session), plus a
working **login page**. The main page is a blank placeholder, so the next plan only
adds features and doesn't have to touch the foundations.

### 1.2 In scope

| Area | What is delivered |
| --- | --- |
| Project | Vite + React + TypeScript app in `frontend/`, scaffolded from the official template, exact pinned versions (section 4) |
| Theme | `theme.css` from the design reference, the Atkinson Hyperlegible Next font from npm, and the rules in `docs/UI-STYLE.md` |
| Store and API | Redux Toolkit store; one RTK Query API slice with base URL, 10-second timeout, `X-User-Id` header, retries for queries only, `refetchOnReconnect` |
| Error handling | One error mapper (any failure → one `ApiError` shape), a global error listener, Sonner toasts, the "can't reach the server" banner and a React error boundary |
| Session | Signed-in user held in a `session` slice and kept in `localStorage`; sign in and sign out |
| Login page | The simplified design: **Sign in** heading, **Email** label, email input, **Sign in** button; every state wired to `POST /auth/login` |
| Main page | Blank, with a slim top bar: wordmark, "Signed in as …", **Sign out** (OQ-F3 ✅) |
| Docs | `docs/UI-STYLE.md` (✅ written 2026-11-11 at the developer's request), a `docs/patterns/frontend.md` skeleton, `AGENTS.md`, `README.md` and `docs/ARCHITECTURE.md` updates (OQ-F2 ✅) |
| Validation | One final validation phase (section 8) |

### 1.3 Out of scope (later plans)

- **Main page features:** grant picker and status filter, grant details with tasks,
  overdue highlighting, **Add task** (with the idempotency key), **Complete**,
  **Close grant**. They need the feature endpoints, which the back-end plan also
  leaves for later (`setup-be-plan.md` §1.3; only `POST /auth/login` exists today).
- How "overdue" reaches the UI (a flag or days overdue from the API, since the UI
  must not compute "today" for rules; Q8). Decided with the main page.
- Front-end tests (Q5: none for now; Vitest + React Testing Library first when
  added).
- Routing, dark mode, translations, analytics.
- Commits and pull requests: the developer makes them, as in the back-end plan
  (OQ-16 there).

---

## 2. Design summary

### 2.1 What the app does on load

```text
main.tsx
  └─ <Provider store>            store reads the saved user from localStorage
      └─ <ErrorBoundary>         render crashes → "Something went wrong" + Reload
          ├─ <OfflineBanner/>    shown while the server is unreachable
          ├─ session.user ? <MainPage/> : <LoginPage/>     (no router, OQ-F4)
          └─ <Toaster/>          Sonner, bottom-right
```

There is no `GET /auth/me` (back-end OQ-11 B), so a saved user is trusted until a
protected request returns `401`, which signs the officer out (section 2.3).

### 2.2 Request flow in the client

```text
component ─ RTK Query hook ─▶ baseQuery
                                ├─ fetchBaseQuery: VITE_API_BASE_URL, timeout 10 s,
                                │  X-User-Id from session (never on its own for login)
                                └─ retry: queries only; FETCH_ERROR, TIMEOUT_ERROR, 5xx;
                                   up to 3 retries with backoff
          ◀─ result / error
error ─▶ toApiError()  ─▶ one ApiError shape
          ├─ component: shows inline errors (fields, refused actions)
          └─ global listener: banner / toast / sign-out (section 2.3)
```

### 2.3 Where errors go (the agreed rule)

**Errors the officer can fix go next to what caused them; errors they can't fix go
in a toast; a lost connection goes in the banner.** The global listener routes by
error **code** rather than only by status, because some `400`s (e.g.
`MALFORMED_REQUEST`) are client bugs the officer can't fix.

| Failure | `ApiError` | Global listener | Component |
| --- | --- | --- | --- |
| Network down, CORS failure, 10 s timeout | `unreachable` | Show the banner | Nothing extra; the officer presses the same button again |
| `400 VALIDATION_FAILED` | `problem` + `fieldErrors` | Nothing | Error under the matching field |
| `401 USER_REQUIRED` / `UNKNOWN_USER` on login | `problem` | Nothing | "No officer uses this email…" under the field |
| `401` on any other endpoint | `problem` | Sign out, reset the API cache, neutral toast "You've been signed out." | Nothing |
| `404 NOT_FOUND`, `409` business rules | `problem` | Nothing | Inline alert at the action (main page, later) |
| `400 MALFORMED_REQUEST`, `IDEMPOTENCY_KEY_REQUIRED`, `422 IDEMPOTENCY_KEY_MISMATCH` | `problem` | Error toast with the reference | Nothing |
| `5xx`, `UNEXPECTED_ERROR`, non-JSON response | `problem` / `unexpected` | Error toast with the reference | Nothing |
| Any successful API response | none | Hide the banner | none |
| Crash while rendering | none | none | Error boundary fallback |

Toast rules (from `docs/UI-STYLE.md` §7): bottom-right, at most 3, close button;
**error toasts stay until closed**; others close after about 5 seconds; the toast
id is `endpointName:code`, so retries of the same failure share one toast.

### 2.4 Back-end contract used (as implemented today)

| Item | Value |
| --- | --- |
| API base URL (dev) | `http://localhost:5080` (`launchSettings.json`, profile `http`) |
| CORS | Allows origin `http://localhost:5173`; headers `X-User-Id`, `Idempotency-Key`, `Content-Type` |
| Sign in | `POST /auth/login` body `{ "email": "…" }` → `200 { id, name, email }`; no `X-User-Id` needed |
| Every other endpoint | Requires `X-User-Id: <user id>` |
| Error body | ProblemDetails with `status`, `title`, `code`, `traceId`; validation errors also have `errors: { field: [messages] }` |
| Codes | `VALIDATION_FAILED`, `MALFORMED_REQUEST`, `USER_REQUIRED`, `UNKNOWN_USER`, `NOT_FOUND`, `IDEMPOTENCY_KEY_REQUIRED`, `IDEMPOTENCY_KEY_MISMATCH`, `UNEXPECTED_ERROR` (BR codes arrive with the features) |
| Seeded users | `alex.nguyen`, `priya.sharma`, `sam.taylor`, `jordan.lee`, `maria.garcia` `@example.org` |

---

## 3. Confirmed decisions

### 3.1 From `ideal.md` (not re-argued)

- **Stack (4.1, Q6):** React + TypeScript on **Vite**, no meta-framework; **Redux
  Toolkit + RTK Query**, used mainly for RTK Query. No extra Redux slices without a
  concrete need.
- **Unreliable network (Q17):** about **10 s timeout**; a **persistent banner** on
  `FETCH_ERROR`/`TIMEOUT_ERROR`; **automatic retry with backoff for queries** plus
  `refetchOnReconnect`; **manual retry for mutations** (delivered as "press the same
  button again", D-9); the idempotency key is created **once per user intent**,
  reused on retry, **never generated in `prepareHeaders`**.
- **Error contract (Q16):** ProblemDetails with `code` and `traceId`; the UI maps
  `code` to a message.
- **Support reference (Q14 D):** show the **first 8 characters** of `traceId` with a
  **copy-full** button.
- **Identity (Q15):** email-only sign-in via `POST /auth/login`; `X-User-Id` on every
  other request; identification, not authentication.
- **Dates (Q8):** calendar dates stay `yyyy-MM-dd` strings, never `Date` objects;
  timestamps from the API carry `Z`. (No dates on screen in this base.)
- **Tests (Q5):** no front-end tests for now.
- **Docs (Q6, Q21):** a front-end pattern doc with the RTK Query boilerplate,
  written as a skeleton when the base exists.

### 3.2 Decided in discussion (2026-11-11 and 2026-11-12)

| # | Decision | Notes |
| --- | --- | --- |
| D-1 | **Two pages:** a separate login page and a main page | Resolves Q15(b) (sign-in UI). `ideal.md` recommended an inline "Sign in as" box; the developer chose a separate page |
| D-2 | **Main page is blank** in this base, apart from the top bar (OQ-F3) | Grants and tasks come in a later plan |
| D-3 | **Theme "register and highlighter"** | Palette, type, components: `docs/UI-STYLE.md`; tokens in `theme.css` |
| D-4 | **Login page, simplified:** **Sign in** heading, **Email** label, email input, **Sign in** button | Centred single column, no card, no wordmark or helper text. The earlier version with a ruled "register sheet" was dropped |
| D-5 | **Toasts with Sonner** (MIT) | Chosen over react-hot-toast, react-toastify and a hand-rolled toaster |
| D-6 | **Global error handler** routing errors to inline / toast / banner (section 2.3) | Refines the developer's "toast for every backend error" idea; accepted (OQ-F5) |
| D-7 | **Style doc in `docs/`** using `style-guide.html` and `theme.css` as references | ✅ Written: `docs/UI-STYLE.md` |
| D-8 | **Error boundary** for render crashes | About 20 lines, no library |
| D-9 | **Banners and toasts only deliver the message.** No "Try again" or "Retry" buttons anywhere; to retry, the officer presses the same button again (OQ-F6) | This is how Q17's "manual retry for mutations" is delivered. Toasts keep only **Close** and **Copy full reference** |

### 3.3 Engineering details chosen in this plan

| Topic | Choice | Why |
| --- | --- | --- |
| Page switching | `session.user ? <MainPage/> : <LoginPage/>`, no router for now (OQ-F4) | Two pages, no URLs to share; avoids a dependency |
| Linting | Keep the template's **oxlint**, not ESLint | The current `create-vite` template ships TypeScript 7 + oxlint; `typescript-eslint` 8.72 supports only TypeScript `<6.1` |
| Versions | Exact pins (`.npmrc` `save-exact=true`) and a committed `package-lock.json`; install with `npm ci` | Same "pinned and reproducible" approach as the back end's central package versions |
| Dev server | Port **5173** with `strictPort: true` | CORS allows only 5173; failing fast beats silently moving to 5174 and getting CORS errors |
| API URL | `VITE_API_BASE_URL` in `frontend/.env.development` (committed; no secrets) | One place to change; no Vite proxy (back-end OQ-15 A: CORS) |
| Font | `@fontsource-variable/atkinson-hyperlegible-next`, imported in `main.tsx` | Self-hosted, works offline; no request to Google |
| Toasts | `<Toaster position="bottom-right" visibleToasts={3} closeButton toastOptions={{ unstyled: true, classNames }}/>` with classes from `theme.css` | Sonner handles stacking and timing; our CSS controls the look |
| Retry | `retry(fetchBaseQuery…, { retryCondition })`: only when `baseQueryApi.type === 'query'`, the error is `FETCH_ERROR`/`TIMEOUT_ERROR`/`5xx`, and attempt ≤ 3 | Mutations are never resent automatically (Q17) |
| Session storage | `localStorage` key `grant-tracker.user`, `{ id, name, email }`, shape-checked on load (bad data is ignored) | Survives reloads; holds no secret |
| Sign out | Clear the session, `api.util.resetApiState()` | The next officer never sees cached data from the previous one |
| Client email check | Required, then a simple `name@domain.tld` pattern; trimmed. The server stays the authority (`UNKNOWN_USER`) | Catches typos before a request |
| Retry by pressing again (D-9) | Banner and toasts have no action buttons. On login, **Sign in** is the retry. For later mutations, the form keeps its idempotency key while its values are unchanged, so pressing the button again reuses it; the key is replaced after success or when the officer edits the form | Pressing again after a lost response replays the original result instead of creating a duplicate (OP-3); an edited form is a new intent, so it gets a new key instead of a `422` |
| Components | Plain elements with theme classes; extract a component only on the second use | Keeps the base small and every line explainable |

---

## 4. Dependencies and tooling (✅ approved 2026-11-12, OQ-F1)

Versions checked on npm on 2026-11-11. Local Node is **v24.11.0** (Vite 8 needs
`^20.19.0 || >=22.12.0`).

| Package | Version | Licence | Kind | Why |
| --- | --- | --- | --- | --- |
| `react`, `react-dom` | 19.3.1 | MIT | runtime | UI |
| `@reduxjs/toolkit` | 2.14.0 | MIT | runtime | Store + RTK Query (Q6) |
| `react-redux` | 9.3.1 | MIT | runtime | React bindings for the store |
| `sonner` | 2.0.8 | MIT | runtime | Toasts (D-5, ✅ chosen by the developer) |
| `@fontsource-variable/atkinson-hyperlegible-next` | 5.3.0 | OFL-1.1 | runtime | Self-hosted font (D-3) |
| `vite` | 8.3.8 | MIT | dev | Dev server and build |
| `@vitejs/plugin-react` | 6.3.0 | MIT | dev | React support for Vite |
| `typescript` | 7.0.2 | Apache-2.0 | dev | Type checking (template default) |
| `oxlint` | 1.93.0 | MIT | dev | Linting (template default) |
| `@types/react`, `@types/react-dom` | 19.3.0 | MIT | dev | Types |
| `@types/node` | 24.19.2 | MIT | dev | Types for `vite.config.ts` |

Scaffolded with `create-vite@9.3.4` (`react-ts` template), then pinned. If TypeScript
7 causes a tooling problem, fall back to `typescript@6.0.3` (risk R-1).

> **Windows note:** on this machine PowerShell blocks `npm.ps1` (execution policy).
> Commands in this plan use `npm.cmd`; the README will say so.

---

## 5. Folder and file layout (target)

```text
frontend/
  .env.development            VITE_API_BASE_URL=http://localhost:5080
  .npmrc                      save-exact=true
  index.html                  <title>Grant Task Tracker</title>, lang="en"
  package.json / package-lock.json
  tsconfig*.json, vite.config.ts (port 5173, strictPort), .oxlintrc.json (template)
  src/
    main.tsx                  font + theme imports; Provider, ErrorBoundary, App, Toaster
    App.tsx                   OfflineBanner + (user ? MainPage : LoginPage)
    vite-env.d.ts             typed VITE_API_BASE_URL
    app/
      store.ts                configureStore, listener middleware, RootState, AppDispatch
      hooks.ts                useAppDispatch, useAppSelector
    api/
      baseApi.ts              createApi, baseQuery (timeout, X-User-Id, retry), tags
      apiError.ts             ApiError type, toApiError(), shortReference()
      errorMessages.ts        code → officer-facing text; endpoint → action phrase
      errorListener.ts        global routing to banner / toast / sign-out (section 2.3)
    features/
      session/
        sessionSlice.ts       user, signedIn, signedOut; localStorage load/save
        authApi.ts            login mutation (injectEndpoints)
        LoginPage.tsx, LoginPage.css
      connection/
        connectionSlice.ts    serverUnreachable flag for the banner
        OfflineBanner.tsx
      main/
        MainPage.tsx, MainPage.css   top bar + empty <main>
    components/
      ErrorBoundary.tsx
      Reference.tsx           8-character reference + "Copy full reference"
      notify.ts               notify.error / notify.info / notify.success over Sonner
    styles/
      theme.css               copied from .github/workflows/setup-repo/theme.css
```

The template's demo files (`App.css`, `assets/`, `public/vite.svg`, counter code) are
removed.

---

## 6. Key component designs

### 6.1 `ApiError` and `toApiError()` (`api/apiError.ts`)

```ts
type ApiError =
  | { kind: 'unreachable'; timedOut: boolean }
  | { kind: 'problem'; status: number; code: string; title: string;
      traceId?: string; fieldErrors: Record<string, string[]> }
  | { kind: 'unexpected'; status?: number; traceId?: string };
```

- `FETCH_ERROR` → `unreachable`; `TIMEOUT_ERROR` → `unreachable` + `timedOut`.
- A JSON body with `code` → `problem`. Field-error keys are lower-cased so `Email`
  and `email` match the same field.
- `PARSING_ERROR` (e.g. an HTML error page) or a body without `code` →
  `unexpected`.
- `shortReference(traceId)` → the first 8 characters.

### 6.2 Base query and API slice (`api/baseApi.ts`)

- `fetchBaseQuery({ baseUrl: import.meta.env.VITE_API_BASE_URL, timeout: 10_000,
  prepareHeaders })`. `prepareHeaders` adds `X-User-Id` when a user is signed in and
  **never** creates idempotency keys.
- Wrapped in `retry(…, { retryCondition })` as in section 3.3.
- `createApi({ reducerPath: 'api', baseQuery, tagTypes: [], refetchOnReconnect: true,
  endpoints: () => ({}) })`. Features add endpoints with `injectEndpoints`. Tags
  arrive with the main page.

### 6.3 Global error listener (`api/errorListener.ts`)

A `createListenerMiddleware` listener on `isRejectedWithValue` actions from the API
slice. It reads `action.meta.arg.endpointName`, maps `action.payload` with
`toApiError()`, and applies the table in section 2.3. A second listener on fulfilled
API actions clears the banner. Messages and action phrases come from
`errorMessages.ts` (e.g. `login` → "Sign-in failed"), with a general fallback
("That didn't work").

### 6.4 Session (`features/session`)

- `sessionSlice`: `{ user: UserDto | null }`, actions `signedIn(user)` and
  `signedOut()`. The initial state is read from `localStorage` and shape-checked.
- A listener writes the user to `localStorage` on `signedIn` and removes it on
  `signedOut`; it also dispatches `api.util.resetApiState()` on `signedOut`.
- `authApi.login` is a mutation: `POST /auth/login` with `{ email }`.

### 6.5 Login page (`features/session/LoginPage.tsx`)

Matches [login.html](./login.html):

| State | Behaviour |
| --- | --- |
| Default | Focus on the email input |
| Empty on submit | Inline "Enter your email."; focus returns to the input |
| Bad format | Inline "Enter an email in the format name@example.org." |
| Pending | Button disabled, reads "Signing in…" |
| `401 UNKNOWN_USER` | Inline "No officer uses this email. Check the spelling, or ask an administrator to add you." |
| `400 VALIDATION_FAILED` | The server's `email` message inline |
| Unreachable | Banner (global); the field keeps its value |
| `5xx` / unexpected | Error toast with reference (global); nothing inline |
| Success | `signedIn(user)` → the main page renders |

Accessibility: `<label for>`, `type="email"`, `autocomplete="email"`,
`aria-invalid` + `aria-describedby` on error, `noValidate` so our messages replace
the browser's.

### 6.6 Main page (`features/main/MainPage.tsx`)

A top bar with the wordmark, "Signed in as {name}" and a quiet **Sign out** button
(dispatches `signedOut`). Below it an empty `<main>` with a visually hidden `<h1>`
"Grant Task Tracker", so screen readers still get a page heading.

### 6.7 Toasts, banner, error boundary

- `notify.error({ id, title, traceId })` → `toast.error(title, { id, description:
  <Reference/>, duration: Infinity })`; `notify.info(title, description)` and
  `notify.success(title)` use about 5 s.
- `OfflineBanner` reads `connection.serverUnreachable`; it shows the message only
  (no button, D-9), uses `role="alert"` and the copy from `docs/UI-STYLE.md` §7.
- `ErrorBoundary`: a class component; the fallback says "Something went wrong on
  this page. Reload the page to continue." with a **Reload page** button. It logs
  the error to the console. There is no `traceId`, because nothing reached the
  server.

---

## 7. Phased work breakdown

Checkboxes are the single source of truth for progress.

### Phase 0: Approvals

- [x] Developer approves this plan and gives the go-ahead to implement (2026-11-12).
- [x] Developer approves the dependencies in section 4 (OQ-F1, 2026-11-12).
- [x] Developer approves the `docs/`, `AGENTS.md` and `README.md` changes in Phase 6
      (OQ-F2, 2026-11-12).
- [x] Links fixed for the plan folder's location, `.github/workflows/setup-repo/`
      (OQ-F7, 2026-11-12): `AGENTS.md` (repository layout and documentation map),
      `docs/ARCHITECTURE.md` (decision-log links), `setup-be-plan.md` (links to
      `docs/`).

### Phase 1: Scaffold

- [x] `npm.cmd create vite@9.3.4 frontend -- --template react-ts` at the repository
      root.
- [x] Add `.npmrc` (`save-exact=true`); pin every version in section 4; install the
      runtime packages; commit `package-lock.json`.
- [x] `vite.config.ts`: `server: { port: 5173, strictPort: true }`.
- [x] `.env.development` with `VITE_API_BASE_URL`; type it in `vite-env.d.ts`.
- [x] Remove the template demo files; set `<title>` and `lang="en"`.
- [x] Check that the root `.gitignore` covers `frontend/node_modules` and
      `frontend/dist`.

### Phase 2: Theme and global UI

- [x] Copy `theme.css` to `src/styles/theme.css`; import it and the font in
      `main.tsx`.
- [x] `ErrorBoundary`, `Reference`, `notify`, `<Toaster>` with theme classes.
- [x] Check Sonner's unstyled DOM against the `.toast*` classes and adjust selectors
      if needed (close button position, title and description elements).

### Phase 3: Store, API and error handling

- [x] `store.ts`, `hooks.ts`, `baseApi.ts` (timeout, header, retry condition).
- [x] `apiError.ts`, `errorMessages.ts`.
- [x] `connectionSlice` + `OfflineBanner`.
- [x] `sessionSlice` with `localStorage` load/save and the sign-out cache reset.
- [x] `errorListener.ts` implementing section 2.3.

### Phase 4: Login page

- [x] `authApi.login`; `LoginPage` with every state in section 6.5.
- [x] `App.tsx` switches between pages on `session.user`.

### Phase 5: Main page (blank)

- [x] `MainPage` with the top bar and **Sign out**.

### Phase 6: Docs (approved `docs/` changes only)

- [x] `docs/UI-STYLE.md` written (D-7, 2026-11-11).
- [x] Point `docs/UI-STYLE.md`'s token link at `frontend/src/styles/theme.css`.
- [x] `docs/patterns/frontend.md` skeleton:
  - add an endpoint with `injectEndpoints`;
  - tags and invalidation;
  - passing an idempotency key as a mutation argument (created per user intent,
    kept while the form is unchanged so pressing the button again reuses it,
    never in `prepareHeaders`; D-9);
  - `toApiError` and the inline / toast / banner rule (no retry buttons in
    banners or toasts; D-9);
  - when (not) to add a Redux slice;
  - using theme classes.
- [x] `AGENTS.md`:
  - add `frontend/` to the repository layout;
  - documentation-map rows for `docs/UI-STYLE.md` and `docs/patterns/frontend.md`
    (no longer "later").
- [x] `README.md`: how to run the front end (`cd frontend`, `npm.cmd ci`,
      `npm.cmd run dev`, API on 5080 first), the `npm.ps1` note, and "identification,
      not authentication".
- [x] `docs/ARCHITECTURE.md` §1: replace "planned separately" with one sentence and
      links to the two front-end docs.

### Phase 7: Validation

- [x] Run section 8.1 and fix anything that fails.
- [x] Hand over section 8.2 to the developer.

---

## 8. Validation

### 8.1 Agent runs (from `frontend/`)

| Check | Command / method | Pass when |
| --- | --- | --- |
| Clean install | `npm.cmd ci` | No errors; lock file unchanged |
| Types and build | `npm.cmd run build` (`tsc -b && vite build`) | Exit code 0, no type errors |
| Lint | `npm.cmd run lint` | No errors |
| Login, success | API (`dotnet run --project backend/src/GrantTracker.Api --launch-profile http`) + `npm.cmd run dev`, browser | `ALEX.Nguyen@Example.org ` signs in; the main page shows "Signed in as Alex Nguyen" |
| Login, errors | Browser | Empty, bad format, unknown email each show the right inline text; no toast |
| Session | Browser | Reload keeps the session; Sign out returns to login and clears `localStorage` |
| Offline | Stop the API, submit | Banner appears; it disappears after the API is back and Sign in succeeds |
| Server error | Browser automation answers `/auth/login` with a `500` ProblemDetails | One error toast with an 8-character reference that stays until closed; copy works |
| Accessibility basics | Keyboard only; 320px wide | All controls reachable with visible focus; layout fits |

### 8.2 Human checks

- The login page matches [login.html](./login.html) closely enough.
- Toast, banner and inline error wording reads well to an officer.
- With a screen reader (optional): field errors and the banner are announced.
- Run `npm.cmd run dev` from a clean clone using only the README.

---

## 9. Risks and mitigations

| # | Risk | Mitigation |
| --- | --- | --- |
| R-1 | TypeScript 7 is new; some tooling may not support it | Using the template's own toolchain (oxlint, not typescript-eslint); fallback pin `typescript@6.0.3` |
| R-2 | Vite moves to another port → CORS errors that look like "server unreachable" | `strictPort: true`; README names port 5173 |
| R-3 | Mutations resent by automatic retry | `retryCondition` allows queries only |
| R-4 | A future endpoint generates the idempotency key in `prepareHeaders` | Forbidden in `docs/patterns/frontend.md`; key passed as a mutation argument |
| R-5 | Toast noise from retries or repeated failures | Toast id per `endpointName:code`; at most 3 visible |
| R-6 | Sonner's unstyled DOM differs from the mockup's classes | Phase 2 check; versions pinned |
| R-7 | A saved user id no longer exists (database replaced) | The first protected request returns `401` → sign out with a toast. Seeded ids are fixed, so a normal reset keeps them (back-end OQ-18) |
| R-8 | `npm.ps1` blocked by execution policy on Windows | Use `npm.cmd`; documented in the README |

---

## 10. Known limitations of this plan

- **No front-end tests** (Q5). Error routing and login states are checked by hand
  and with browser automation only.
- **The `401` sign-out path can't be exercised yet:** the blank main page makes no
  protected requests. It's implemented now and verified with the first main-page
  feature.
- **Query retry and `refetchOnReconnect` aren't exercised** for the same reason.
- **A saved session is trusted until the next protected request** (no
  `/auth/me`, by back-end decision OQ-11).
- **The email format check is deliberately simple**; the server decides whether the
  officer exists.
- **Sonner announces toasts politely** (an `aria-live="polite"` region), so error
  toasts don't interrupt a screen reader. Inline errors and the banner use
  `role="alert"`.
- **`localStorage` can be edited by anyone at the keyboard**, like the email sign-in
  itself; it is identification, not authentication (Q15).

---

## 11. Decision index

| Ref | Topic | Decision |
| --- | --- | --- |
| D-1 to D-9 | Discussion of 2026-11-11 and 2026-11-12 | Section 3.2 |
| OQ-F1 | Dependencies | ✅ Approved as pinned in section 4 |
| OQ-F2 | Docs changes | ✅ Approved: all of Phase 6 |
| OQ-F3 | Top bar on the blank main page | ✅ Yes |
| OQ-F4 | Router | ✅ None for now |
| OQ-F5 | Error routing rule | ✅ Accepted (section 2.3) |
| OQ-F6 | Retry buttons in banners and toasts | ✅ None; they only deliver the message, and pressing the same button again is the retry (D-9) |
| OQ-F7 | Location of the plan folder | ✅ Keep `.github/workflows/setup-repo/`; links fixed |

---

## 12. Open questions (all decided 2026-11-12)

### OQ-F1. Dependencies (section 4)

Approve the package list as pinned. Sonner is already approved (D-5). Everything
else is either the official template's toolchain or required by `ideal.md` (RTK,
React), plus the self-hosted font.

> **✅ Decided (2026-11-12):** approved.

### OQ-F2. Changes to `docs/`, `AGENTS.md` and `README.md` (Phase 6)

`AGENTS.md` asks for developer approval before changing `docs/`. `docs/UI-STYLE.md`
was written at the developer's direct request. Phase 6 also touches
`docs/patterns/frontend.md` (new), `docs/ARCHITECTURE.md` §1, `AGENTS.md` and
`README.md`. **Recommendation:** approve all of Phase 6 together with this plan.

> **✅ Decided (2026-11-12):** approved.

### OQ-F3. Does the blank main page keep a top bar?

Without "Signed in as …" and **Sign out**, there is no way to switch officers or test
signing out. The bar is about 20 lines and stays when the features arrive.
Alternative: a truly empty page, with sign-out added later.

> **✅ Decided (2026-11-12):** yes, keep the top bar.

### OQ-F4. Router?

With two pages and nothing to deep-link, `session.user ? MainPage : LoginPage` is
enough. If later screens need URLs (e.g. `/grants/{id}`), add `react-router` then.

> **✅ Decided (2026-11-12):** no router for now.

### OQ-F5. Error routing rule

The developer proposed "any backend error shows an alarming toast". The plan refines
it (section 2.3): toasts for errors the officer can't fix, inline for errors they
can, the banner for connection loss. The reasons are that Q17 requires a persistent
offline indicator, that field errors belong next to the field, and that too many red
toasts dull real alarms.

> **✅ Decided (2026-11-12):** accepted as in section 2.3.

### OQ-F6. Retry buttons in banners and toasts?

Q17 asks for manual retry of mutations. Options were a "Try again" button in the
banner or toast, or letting the officer press the original button again.

> **✅ Decided (2026-11-12): banners and toasts only deliver the message; to retry,
> the officer presses the same button again** (D-9).
>
> - No "Try again" or "Retry" button in the banner, in toasts or next to failed
>   actions. Toasts keep only **Close** and **Copy full reference**.
> - Messages may say how to retry, e.g. "…then select Sign in again".
> - **Effect on later mutations (OP-3):** the form keeps its idempotency key while its
>   values are unchanged, so pressing the button again after a lost response reuses
>   the key and the server replays the original result. The key is replaced after
>   success or when the officer edits the form (a new intent), which also avoids a
>   `422` key mismatch.
> - `docs/UI-STYLE.md` §6–7 and `style-guide.html` updated to match.

### OQ-F7. Where do the plan files live?

The files moved from `.github/setup-repo/` to `.github/workflows/setup-repo/`, which
broke links in `AGENTS.md`, `docs/ARCHITECTURE.md` and `setup-be-plan.md`. Options
were to move the folder back, or to keep the new location and fix the links.

> **✅ Decided (2026-11-12): keep `.github/workflows/setup-repo/`** (the developer
> updated the folder structure). Links fixed the same day:
>
> - `AGENTS.md`: repository layout and documentation-map paths
>   (`.github/workflows/<feature>/…`);
> - `docs/ARCHITECTURE.md`: decision-log links;
> - `setup-be-plan.md`: links to `docs/ARCHITECTURE.md` (one more `../`).
>
> Note: GitHub Actions only runs `.yml`/`.yaml` files in `.github/workflows/`, so
> the Markdown, HTML and CSS here are never executed.
