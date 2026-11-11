# Feature Workflow & Prompt Style

> A reference for how we drive a feature from idea to implementation. Emulate this
> when starting any feature so you know what to produce at each stage.
>
> **Status: to be re-validated by the lead.** Treat anything unclear as a question
> for review, not an assumption.

---

## The core rule (in the lead's words)

> "We should keep at least **2 files** — one for **idea and abstract** so we can
> track the overall goal — then, if the idea is **green** via daily or leader
> approval, we shall **generate the plan later**."

So a feature always has **two separate documents**, produced at **two separate
stages**, gated by an **approval step**:

1. **`ideal.md`** — idea & abstract. Written first. Tracks the overall goal and the
   decisions. **No implementation detail.**
2. **`plan.md`** — implementation plan. Written **only after** the idea is approved
   ("green") in daily standup or by the leader.

**Never jump straight to a plan or to code.** The idea must be green first.

---

## The stages

### Stage 1 — Idea & abstract (`ideal.md`)
Goal: get alignment on *what* and *why*, and record decisions — before any *how*.

Write this first, present it, and iterate until it is approved. It should contain:
- **Context** — how the relevant area works today (trace the real code, cite files).
- **The ideal** — the abstract goal, kept product-/implementation-neutral.
- **Divergence axes** — where reasonable designs differ, and the trade-off of each.
- **Decision** — what we are committing to, and the rejected alternatives + why.
- **Known limitations / open questions** — honestly listed, not hidden.

It must **not** contain a step-by-step implementation plan, file-by-file changes,
or task checklists. Keep it a decision record.

### Gate — get it "green"
The idea is reviewed in **daily standup or by the leader**. Only once it is
approved ("green") do we move on. If it is not green, keep iterating on `ideal.md`.

### Stage 2 — Implementation plan (`plan.md`)
Goal: turn the approved idea into an executable plan. Written **only after** the
gate. It should contain:
- **Goal & scope** (explicit in/out of scope).
- **Design summary** — what changes, what stays.
- **Confirmed decisions** (carried from `ideal.md`, no re-litigating).
- **Phased work breakdown** with checkboxes as the single source of truth for
  progress.
- **Validation** — what the agent runs vs. what the human verifies.
- **Risks & mitigations**, and **known limitations** of the plan.

### Gate — plan approval, then implement
Only start coding after the plan is approved. Any code-changing work waits for an
explicit go-ahead.

---

## How to reference this when doing a feature

Ask yourself, in order:

1. **Do I have an approved `ideal.md`?**
   - No → write/refine `ideal.md` (idea & abstract only). Present it. Do not plan
     or code yet.
   - Yes → continue.
2. **Is the idea green (daily / leader approval)?**
   - No → keep it at the idea stage.
   - Yes → continue.
3. **Do I have an approved `plan.md`?**
   - No → generate `plan.md` from the approved idea. Present it. Do not code yet.
   - Yes → continue.
4. **Implement**, following the plan's phases and ticking checkboxes as you go.

---

## Working style expectations (observed)

- **Consultant/architect first.** Analyse and plan before implementing; prefer
  understanding over rushing to code.
- **Trace the real code before claiming how something works.** Cite concrete files
  and flows; don't describe from assumption.
- **Separate "what/why" from "how".** Idea docs stay abstract; plans hold the
  mechanics.
- **Surface trade-offs and rejected options**, not just the chosen path.
- **Be honest about limitations and open questions** — list them explicitly.
- **Confirm decisions before locking them in**, and record them so they aren't
  re-argued later.
- **Gate on approval.** Idea → approval → plan → approval → code. Don't skip a gate.
- **Defer non-essential artifacts** (e.g. supporting docs) until the core solution
  is confirmed/green, rather than writing them up front.

---

## Name the problem, then the solution's common name (the lead's method)

> "From the feature/problem we validate solutions first — their common-ish
> name, design pattern, and lib support. When you want to add context via
> variables to a text file and context is provided dynamically, it *sounds like a
> template engine*, which I know is best solved in C# by Scriban. If it were
> Shopify, I'd first need to know what a template engine is, then search for a lib
> called Liquid and prompt further on applying it. Best is when the lib is common,
> widely used, and commercially free."

The method — use it before committing to a design:

1. **Describe the problem in plain terms** (e.g. "inject dynamic values into a text
   body, values supplied at runtime").
2. **Find the problem/solution's common name — the category.** Here the keyword was
   **"template engine."** Naming the category is the unlock; if you don't know it,
   that is the thing to research first (ask, search, or prompt the AI for "what is
   the general name for X?").
3. **Find the common, well-supported lib for that category on your platform**
   (C# template engine → Scriban; if the target were Shopify → Liquid).
4. **Prefer a lib that is common, widely used, actively maintained, and
   commercially free** over a clever bespoke solution.

**Why it matters:** the hard part is often not the code but *recognising the
category*. Once you can name it ("template engine", "state machine", "outbox
pattern", "rate limiter"), the design patterns and battle-tested libraries are a
short search away — and you avoid reinventing something that already has a standard
answer.

> Worked example (this feature): problem = "dynamic variables in an email body" →
> category = **template engine** → C# best-supported lib = **Scriban** (BSD-2,
> most popular/active). If you didn't already know "template engine," that keyword
> is exactly what to surface and validate first.

---

## File conventions

- Keep the two files together per feature, e.g. under a feature-named folder:
  `.../<feature-name>/ideal.md` and `.../<feature-name>/plan.md`.
- **`ideal.md`** = overview of idea + decisions, no implementation plan.
- **`plan.md`** = implementation plan, generated only after the idea is green;
  checkboxes track progress.

---

> **Re-validation note (for the lead):** please confirm — (1) file names
> (`ideal.md` / `plan.md`) and folder layout, (2) what counts as "green" (daily
> mention vs. explicit leader sign-off), and (3) whether the plan also needs
> approval before coding, or only the idea.
