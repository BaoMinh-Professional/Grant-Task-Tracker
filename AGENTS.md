# AGENTS.md

## Project overview

Community grants portal for funding local not-for-profit organisations. An organisation applies, a grant record is opened, and a grants officer manages it until it is closed, creating and tracking tasks along the way.

## Tech stack

- Backend: C#, .NET 10, ASP.NET Core
- Frontend: React, TypeScript, Redux Toolkit, RTK Query
- Database: SQLite

## Repository layout

- `backend/src/GrantTracker.Api/`: HTTP endpoints, middleware, configuration and composition root
- `backend/src/GrantTracker.Core/`: domain model, use cases, mediator, behaviours and ports
- `backend/src/GrantTracker.Data/`: EF Core persistence, migrations and seed data
- `backend/tests/`: Core unit, Data integration and Api HTTP tests
- `frontend/`: Vite, React, TypeScript, Redux Toolkit and RTK Query application
- `docs/`: architecture, implementation patterns and data-model illustration
- `.github/workflows/setup-repo/`: approved design decisions and implementation plans

## Documentation map

| File                                    | Holds                                                                                                                                     | Read when                                                            |
| --------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------- |
| `README.md`                             | What the project is; how to start it, test it and reset the database                                                                      | First time in the repo; running the app                              |
| `prompt-style.md`                       | The workflow instructions                                                                                                                 | Before starting any feature                                          |
| `.github/workflows/<feature>/ideal.md`  | A feature's decision record (options, trade-offs, why)                                                                                    | Before changing anything that feature decided; when asked "why"      |
| `.github/workflows/<feature>/*plan*.md` | A feature's implementation steps and progress checkboxes                                                                                  | While implementing that feature                                      |
| `docs/ARCHITECTURE.md`                  | Layers, project references, request flow, error contract, cross-cutting concerns                                                          | Before structural changes; when unsure where code belongs            |
| `docs/patterns/backend.md`              | Recipes: add a use case, rule, endpoint or test; idempotency; audit; clock                                                                | Before writing back-end code                                         |
| `docs/patterns/frontend.md`             | RTK Query endpoints, tags, idempotency, errors, state and theme                                                                           | Before writing front-end code                                        |
| `docs/UI-STYLE.md`                      | Front-end palette, typography, components, accessibility and writing                                                                      | Before building or changing front-end UI                             |
| `docs/ENTITIES.json`                    | Illustration of the database design (DBML syntax), kept in sync with the schema; the EF configurations and migrations are the real schema | When changing entities or migrations; when explaining the data model |

## Working agreement

- Keep changes surgical and match existing style.
- Update docs when behaviour changes.
- Don't commit secrets or connection strings with credentials.
- Don't change `docs/` without developer approval; the back-end implementation plan explicitly approves its documented deliverables.
- Ask before adding dependencies or tooling.
