# AI Usage

## Tools and models

I used the AI assistant integrated with VS Code throughout the exercise. I
selected models and reasoning levels according to the task:

| Work                                                                  | Model and reasoning level                           |
| --------------------------------------------------------------------- | --------------------------------------------------- |
| Explore the exercise and understand its requirements                  | Haiku 5.5 — High                                    |
| Define ideas, requirements, goals and decisions for the initial setup | Opus 5.5 — High                                     |
| Define ideas, requirements, goals and decisions for later features    | Sonnet 5.5 — High                                   |
| Plan the initial setup                                                | Opus 5.5 — High                                     |
| Plan later features                                                   | Sonnet 5.5 — High                                   |
| Implement from an approved, detailed plan                             | GPT-6 Luna — None                                   |
| Trace and fix bugs                                                    | GPT-6 Luna — High/Medium or Haiku 5.5 — High/Medium |
| Research and answer focused technical questions                       | GPT-6 Luna — High/Medium or Haiku 5.5 — High/Medium |

The intent was to use higher reasoning for exploration and design, then rely on
the detailed approved plans to keep implementation focused without requiring a
high-reasoning model for every coding step.

## Agent setup and workflow

- [`AGENTS.md`](AGENTS.md) provides the overall project context: purpose, stack,
  repository layout, documentation map and working agreement.
- [`prompt-style.md`](prompt-style.md) is the most important AI-workflow
  instruction. It guides how I work with AI during discovery and planning: first
  define the idea and decisions in an `ideal.md`, get approval, then write a
  detailed implementation plan and get approval before coding.
- The records under [`.github/workflows/`](.github/workflows/) capture
  feature-specific research, options and trade-offs, open questions, decisions,
  scope, implementation steps and validation. In particular,
  [`setup-repo/`](.github/workflows/setup-repo/) records the initial setup and
  [`grant-task-management/`](.github/workflows/grant-task-management/) records
  the later feature work.

My workflow was to explore the exercise, use AI to develop and compare options,
decide and record requirements, then approve a detailed step-by-step plan before
asking AI to implement it. I used focused follow-up sessions to trace behavior,
understand unfamiliar parts of the code and investigate issues. I reviewed the
results against the requirements and test output.

I did not use sub-agents. I used the tool permissions available in my VS Code
environment and did not add separate permission rules for this project.

## Prompts

I used prompts not only to get answers, but to understand the reasoning and
trade-offs behind the implementation:

> “Idempotency to solve the OP-3 so which way you think is better”

> “Explain optimistic concurrency with a concrete two-officer example. Show how the task version, ETag and `If-Match` prevent a stale edit from overwriting newer work.”

> "Do not make assumption, any issues, anything needs to resolve go to section called "Open Questions" section. Each question explain in detail, why it matters, how it affects, provide me options with pros and cons and a recommendation."

## Example of an AI mistake

While investigating why there could be more audit rows than idempotency records,
an AI explanation incorrectly said that `IdempotencyBehaviour` called the
handler first and stored the idempotency key afterward. I questioned that
explanation and traced the code. The implementation actually reserves and saves
the key before calling the handler, within the command transaction. If the
command is rejected, that transaction rolls back the reservation; the audit
behavior then records the rejected command separately.

The explanation, not the implementation, was wrong. I kept the transactional
design and corrected my understanding of why a rejected request can have an
audit record without a corresponding idempotency record. This reinforced the
need to verify AI explanations against the source rather than relying on a
plausible description.

## Human in the Loop

The project was produced with AI assistance. I supplied the ideas,
requirements and decisions, answered design questions, reviewed the proposed
results and checked them against the exercise. I did not substantially write or
rewrite the implementation code myself.
