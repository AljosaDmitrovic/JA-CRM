# Job Application CRM (JaCrm)

Personal, single-user CRM for a job search: companies → vacancies → applications, with a Kanban
pipeline, status history, salary normalization across currencies, and JSON/CSV export/import.

- **Requirements:** [`job-application-crm-prompt.md`](job-application-crm-prompt.md), the source of truth for scope.
- **Architecture plan:** [`docs/architecture.md`](docs/architecture.md).
- **Decisions and assumptions:** [`docs/decisions.md`](docs/decisions.md). Every interpretation of the
  spec is logged there as `D-nn`. Add new ones at the end of the matching section, and keep the numbers unique.
- **Status:** see "Progress" at the bottom of this file. Update it at the end of every phase.

## Stack

ASP.NET Core 10 Minimal APIs + EF Core 10 + SQLite (`src/JaCrm.Api`), Angular 22 standalone +
signals + zoneless + Angular CDK, hand-written SCSS tokens (`web/`), xUnit v3 (`tests/`),
Playwright (`e2e/`). One Docker image serves the API under `/api` and the Angular build for every
other path.

## Commands

```bash
# Backend (needs a password hash; see below)
dotnet build JaCrm.slnx
dotnet test --solution JaCrm.slnx            # unit + integration (real SQLite files, no mocks)
Auth__PasswordHash='<hash>' dotnet run --project src/JaCrm.Api   # http://localhost:5080
echo 'my-password' | dotnet run --project src/JaCrm.Api --no-launch-profile -- hash-password
dotnet run --project src/JaCrm.Api -- seed   # demo data, only into an EMPTY database
dotnet tool run dotnet-ef migrations add <Name> --project src/JaCrm.Api --output-dir Data/Migrations

# Frontend (Node 22 via nvm: `source ~/.nvm/nvm.sh && nvm use 22`)
cd web && npm start                          # http://localhost:4200, proxies /api to :5080
cd web && npx ng build                       # production build (budgets enforced)

# Everything in Docker
docker compose up -d --build                 # http://localhost:8080, data in volume jacrm-data
```

`dotnet run` prints "Using launch settings…" on stdout, so pass `--no-launch-profile` when you
capture the output of `hash-password`.

```bash
# End-to-end (Playwright, Chromium). Needs a production build in web/dist first.
cd web && npx ng build && cd ../e2e && npm test   # starts the API itself on :5181 with a fresh e2e/.data
```

**Known machine issue (owner's Mac):** the repo lives on `~/Desktop`, which is synced with iCloud
Drive with "Optimize Mac Storage". When the disk runs low, macOS evicts files (they become
`dataless` and reading them blocks until iCloud downloads them). Symptoms: `ng build`, `dotnet`,
`git` or the API hang with no output. Check with `find . -flags +dataless -not -path '*/node_modules/*' | head`.
Fix: delete and reinstall `node_modules` / `bin` / `obj`, and read the rest back
(`find . -flags +dataless -type f -print0 | xargs -0 -n1 -P8 cat >/dev/null`).

## Layout

```
src/JaCrm.Api/
  Domain/      Entities + pure rules (no EF queries, no ASP.NET): ApplicationStatuses (status→phase,
               auto submission date), SalaryNormalizer, CompanyMatching, DashboardCalculator,
               Application (Create/ChangeStatus write history), PostingSnapshot
  Data/        AppDbContext, Configurations/EntityConfigurations.cs, Migrations/, DevSeeder,
               DatabaseMaintenance (migrate at startup + VACUUM INTO backups)
  Features/    One file per module: request/response records + <Module>Service + <Module>Endpoints.
               FeatureRegistry.cs registers every module (one line each).
  Common/      Validator (field errors → 400), Errors (exceptions → ProblemDetails), Paging, Text, Clock
  Cli/         hash-password, seed, backup, healthcheck
web/src/app/
  core/        models.ts (API contracts, mirror the C# records), auth (guard + CSRF interceptor),
               meta.service (enum labels/phases from GET /api/meta), settings.store, theme,
               toasts, dialogs, shortcuts
  shared/      Reusable UI (icon, badges, markdown, salary, tag input, confirm dialog, pipes)
  layout/      Shell (sidebar + mobile tab bar), command palette (⌘K), shortcuts help (?)
  features/    dashboard, applications, companies, vacancies, settings, auth
web/src/styles/  _tokens (light/dark), _base, _controls, _patterns
tests/JaCrm.UnitTests        Domain rules
tests/JaCrm.IntegrationTests WebApplicationFactory + temp SQLite file per factory (AppFactory),
                             TestApi helpers for arranging data through the real API
e2e/                         Playwright (pinned to 1.63 = cached Chromium build 1243)
```

## API map

`/api/auth/{login,logout,session}` · `/api/meta` · `/api/settings` (+ `/exchange-rates`) ·
`/api/companies` (+ `/options`, `/duplicates`, `/{id}/archive|unarchive`) ·
`/api/vacancies` (+ `/{id}/convert`) · `/api/applications` (+ `/board`, `/{id}/status`) ·
`/api/dashboard` · `/api/search?q=` · `/api/lookups` ·
`/api/data/export`, `/api/data/export/{entity}.csv`, `POST /api/data/import?confirm=replace-all`.
List endpoints take filters as query parameters (repeat a key for arrays: `?status=A&status=B`).

## Frontend patterns

- List pages keep search, filters, sort and page in the URL (`core/query-state.ts`), and load through
  a `Loadable` (`core/loadable.ts`) inside an `effect`.
- Forms: typed reactive forms. `applyServerErrors(form, error, aliases)` maps 400 field keys onto
  controls, and `controlError()` builds the message.
- Shared filters for vacancies and applications: `shared/filter-panel.ts` (+ `activeFilters()` chips).
- A native `<select>` whose options come from `@for` must mark `[selected]` on the options instead of
  binding `[value]` on the select (the value would be applied before the options exist).
- Absolutely positioned content inside a horizontal scroller needs a positioned scroller (see
  `.board`), or it widens the page.

## Conventions that matter

- **Status changes only through `Application.ChangeStatus`**. It writes the history row, and the
  caller saves once, so the status and its history land in one transaction.
- **Field ownership:** the vacancy owns the posting, and the application owns the pursuit. The
  application's company always comes through `Vacancy.CompanyId` and is never stored twice. The
  posting snapshot is JSON, written once at creation, and never edited.
- **Archive vs delete:** companies and vacancies with references are archived (409 on delete).
  FKs are `Restrict`. Only application → history cascades.
- **Validation:** the server is authoritative. `Validator` collects camelCase field keys
  (`postedSalary.min`), and the Angular forms map them back onto controls with `fieldErrors()`.
- **Errors:** throw `ValidationException` / `NotFoundException` / `ConflictException`, and
  `AppExceptionHandler` turns them into RFC 7807 problem responses.
- **Enums** are stored and serialized by name. Labels and phases come from `GET /api/meta`, so
  the UI never hardcodes them.
- **Money** is `decimal` with currency + period + gross/net/unknown (`SalaryValue`, `SalaryRange`).
  Comparisons use annual gross in the base currency. Net and unknown amounts are flagged, never estimated.
- **Dates:** timestamps are UTC `DateTime`; date-only fields are `DateOnly`. "Today" comes from
  `Clock` in `App__TimeZone`.
- **CSRF:** every unsafe `/api` request needs `X-Requested-With: JaCrm`. The Angular interceptor
  adds it, and tests use `AppFactory.CreateAuthenticatedClientAsync()`.
- **Frontend:** OnPush + signals everywhere, design tokens only (no raw colours), icons only via
  `<app-icon name>` (register new ones in `shared/icon.ts`), every drag-and-drop has a keyboard path.
- Seeding never runs automatically. There are no placeholder buttons and no mock data in features.

## Progress

| Phase | State |
|---|---|
| 1 Plan | Done (`docs/architecture.md`, `docs/decisions.md`) |
| 2 Foundation | Done: solution, schema + migration, auth (cookie, PBKDF2, rate limit, CSRF), design system, shell, command palette, Docker files, test projects |
| 3 Core features | Done: companies, vacancies, applications (board + table, status history), settings + rates, dashboard, search/filters, export/import (JSON + CSV), with unit + integration tests |
| 4 UX polish | Done: responsive board/tables, unsaved-change guards, undo on moves, record titles, touch behaviour |
| 5 QA (incl. Playwright, Docker persistence) | Done except the Docker image check: 177 backend + 3 E2E flows green, restart persistence and SQLite integrity tested. Docker build not run (Colima VM disk full; owner decides) |
| 6 Report + README | Not started |
