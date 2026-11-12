# UI style

> **Status:** design reference for the front end, approved by the developer on
> 2026-11-11. Update it **in the same commit** as any change to the theme
> (`AGENTS.md`).
> **Visual references:**
> [style-guide.html](../.github/workflows/setup-repo/style-guide.html) (every token
> and component on one page) and
> [login.html](../.github/workflows/setup-repo/login.html) (the login page and its
> states). Open them in a browser; they need no build.
> **Tokens:** [theme.css](../frontend/src/styles/theme.css) is the source of truth.

## 1. Direction: "register and highlighter"

The tracker should read like a well-kept paper register of grants and tasks:
sage paper, green-black ink and one council green for actions. The **one bold
device is the highlighter**. Yellow marks only what needs attention right now:
**overdue tasks and keyboard focus**. Nothing else is yellow, so an officer can
spot overdue work at a glance.

Keep everything around the highlighter quiet: flat surfaces, thin rules, no
gradients and no decoration that doesn't carry information.

## 2. Palette

Use the CSS variables, never raw hex values, in components.

| Token | Hex | Use | Contrast |
| --- | --- | --- | --- |
| `--paper` | `#EEF1EC` | Page background | |
| `--sheet` | `#FFFFFF` | Panels, inputs, lists, toasts | |
| `--ink` | `#1D2B26` | Text, strong rules, focus ring | 12.9:1 on paper, 14.7:1 on sheet |
| `--ink-muted` | `#55665F` | Hints, metadata, secondary text | 5.3:1 on paper, 6.1:1 on sheet |
| `--rule` | `#B9C4BD` | Hairlines and quiet borders (decoration only) | 1.8:1, never the only edge of a control |
| `--council` | `#24594A` | Primary buttons, links, selection, "Open" | 8.1:1 with white text, 7.1:1 on paper |
| `--council-strong` | `#1B4639` | Hover and pressed council | 10.6:1 with white text |
| `--council-tint` | `#DCE8E1` | Selected rows, "Completed" tag | council text 6.4:1 |
| `--highlighter` | `#F5D93B` | Overdue and focus **only** | ink text 10.4:1 |
| `--highlighter-soft` | `#FBEFA6` | Field fill while focused | ink text 12.6:1 |
| `--stamp` | `#B0302B` | Errors, rejected actions, High priority | 6.4:1 on sheet, 5.6:1 on paper |
| `--stamp-tint` | `#F6E3E1` | "Can't reach the server" banner | ink text 11.9:1 |

Colour rules:

- **Yellow means "needs attention now".** Don't use it for selection, branding or
  success.
- **Red means "something went wrong or was refused"**, plus the High priority tag.
  Don't use red to mark overdue; that's the highlighter's job.
- **Green means "act here" or "this is fine".**
- **Colour is never the only signal.** Every tag and message also says its meaning
  in words.

## 3. Typography

- **Typeface:** Atkinson Hyperlegible Next for everything (SIL Open Font Licence).
  It keeps 0/O and 1/l/I apart, which matters when an officer reads
  `GR-100001` or a support reference aloud. Fallback: `system-ui, sans-serif`.
- **Scale:** major third (×1.25) from 16px.

| Token | Size | Weight | Use |
| --- | --- | --- | --- |
| `--text-3xl` | 49px | 750 | Login headline |
| `--text-2xl` | 39px | 750 | Login headline on phones |
| `--text-xl` | 31px | 750 | Page headings (grant name) |
| `--text-lg` | 25px | 750 | Section headings |
| `--text-md` | 20px | 700 | Row titles, large inputs |
| `--text-base` | 16px | 400 / 700 | Body; labels and buttons at 700 |
| `--text-sm` | 13px | 400 / 700 | Hints, metadata, tags |

- Body line height 1.5; headings 1.1 with −0.01em tracking.
- Keep body text under about 65 characters per line (`--measure`).
- **Sentence case everywhere.** No all-caps labels, no small "eyebrow" labels above
  headings, no single word picked out in a different colour or style.
- Use `.num` (tabular figures) for dates, counts and references so they line up.

## 4. Space, shape and elevation

- **Spacing:** 4px base. Tokens `--space-1` (4px) to `--space-9` (96px).
- **Corners:** controls 4px (`--radius-control`), tags 2px (`--radius-tag`),
  panels and pages square.
- **Elevation:** everything is flat. The **only** shadow is `--shadow-float`, for
  layers that float above the page (toasts).

## 5. Focus and motion

- **Focus:** a 3px ink outline with a 7px highlighter halo on buttons and links.
  Fields get a highlighter fill that sweeps in, plus a thicker ink underline.
  Never remove focus styles.
- **Motion:** only in answer to the officer's action (the field highlight, button
  press). Nothing animates by itself. `prefers-reduced-motion` turns all
  animation off.

## 6. Components

| Component | Classes | Rules |
| --- | --- | --- |
| Primary button | `.btn .btn-primary` | One per form or view: the main action ("Sign in", "Add task") |
| Secondary button | `.btn .btn-secondary` | Row actions ("Complete", "Close grant") |
| Quiet button | `.btn .btn-quiet` | Small utility actions ("Copy full reference", "Sign out") |
| Field | `.field`, `.field-label`, `.field-hint`, `.input`, `.field-error` | Label above the input; error below it; set `aria-invalid` and `aria-describedby` |
| Tag | `.tag` + `.tag-overdue`, `.tag-due-today`, `.tag-completed`, `.tag-open`, `.tag-closed`, `.tag-high`, `.tag-medium` | Always words, e.g. "Overdue by 10 days", "High priority" |
| Inline alert | `.alert`, `.alert-title` | A refused action, shown where the action was taken |
| Reference | `.reference`, `.reference-code` | First 8 characters of the `traceId`; a copy button copies the full id |
| Banner | `.banner`, `.banner-text` | Full width at the top; only for "Can't reach the server" |
| Toast | `.toast` + `.toast-error` / `.toast-success`, `.toast-title`, `.toast-close` | Sonner in unstyled mode (see 7) |

Buttons show their pending state in words ("Signing in…") and are disabled while
the request runs.

## 7. Where messages go

The rule: **errors the officer can fix go next to what caused them; errors they
can't fix go in a toast; a lost connection goes in the banner.**

| Situation | Where | Example |
| --- | --- | --- |
| Server unreachable or timed out | Banner, stays until a request succeeds | "Can't reach the server. Check your connection, then select Sign in again." |
| `400` validation (`VALIDATION_FAILED`) | Inline under the field | "Enter your email." |
| `401` on login (`UNKNOWN_USER`) | Inline under the email field | "No officer uses this email. Check the spelling, or ask an administrator to add you." |
| `401` on any other request | Sign out, back to the login page, neutral toast | "You've been signed out. Sign in again to continue." |
| `404`, `409` (business rules) | Inline alert at the action | "This grant can't be closed yet." |
| Client mistakes (`MALFORMED_REQUEST`, idempotency-key codes), `5xx`, anything unexpected | Error toast with the reference | "Sign-in failed because of a server error." |
| Success | Success toast, only when the result isn't already visible | "Task added" |

Toast behaviour:

- Bottom-right, at most 3 visible, each with a close button.
- **Error toasts stay until closed**, so the officer has time to copy the reference.
  Success and neutral toasts close after about 5 seconds.
- Identical errors share one toast (same id), so automatic retries don't stack.

**Banners and toasts only deliver the message.** They have no "Try again" or
"Retry" button. To retry, the officer presses the same button again (e.g. **Sign
in**, **Add task**), and the message says so where it helps ("…then select Sign in
again"). The only buttons a toast may have are **Close** and **Copy full
reference**.

## 8. Writing

- Name things the way officers do: "grant", "task", "officer", "reference", not
  "entity", "record" or "trace".
- Buttons say exactly what happens, and the result uses the same word:
  "Add task" → "Task added".
- Errors say what happened and what to do next, in plain words, without apologising.
  Don't show raw status codes or exception text.
- Sentence case, active voice, no exclamation marks.

## 9. Accessibility floor

- WCAG 2.2 AA contrast (section 2 lists the ratios).
- Every input has a visible `<label>`; errors are linked with `aria-describedby`
  and announced (`role="alert"` or a live region).
- Keyboard: everything reachable by Tab, in reading order, with visible focus.
- No time limits on error messages (error toasts don't close by themselves).
- Works from 320px wide; the layout stacks to one column on phones.
- `prefers-reduced-motion` is respected.

## 10. Pages in the base

- **Login:** a single centred column on paper, with no card or panel. It contains
  the **"Sign in"** heading, an **"Email"** label, an email input, and a
  full-width **"Sign in"** button. Field errors appear under the input, the
  banner at the top, and toasts bottom-right.
  See [login.html](../.github/workflows/setup-repo/login.html).
- **Main page:** blank for now, with a slim top bar showing the wordmark,
  "Signed in as …" and a "Sign out" button. The grants and tasks layout comes
  with the main page plan; the sketch at the bottom of
  [style-guide.html](../.github/workflows/setup-repo/style-guide.html) is only a
  direction.

## 11. Changing the style

1. Change or add a token in `theme.css` first; components only use tokens.
2. Show the change in `style-guide.html` so it can be reviewed in a browser.
3. Update this document in the same commit.
4. Check contrast for any new colour pair and add it to section 2.
