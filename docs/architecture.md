# Architecture

Technical plan for the personal Job Application CRM. The product requirements live in
[`job-application-crm-prompt.md`](../job-application-crm-prompt.md); every decision and assumption
made while implementing them is in [`decisions.md`](decisions.md).

## 1. Selected technologies

| Concern | Choice | Why |
|---|---|---|
| Backend | ASP.NET Core 10, Minimal APIs | The owner's main stack. Minimal APIs with one endpoint group per feature keep the code flat: no controllers, no mediator. |
| Persistence | EF Core 10 + SQLite | A single file, nothing to operate, trivial to back up. EF Core migrations are applied at startup. |
| Frontend | Angular 22 (standalone, signals, zoneless) | The owner's other stack. Signals and zoneless change detection keep rendering cheap without a state library. |
| UI primitives | Angular CDK (drag-drop, dialog, menu, overlay, a11y) | Accessible behaviour (focus traps, keyboard menus, drag and drop) without a visual theme to fight. |
| Styling | Hand-written SCSS on CSS custom properties (design tokens) | Full control over the look; light/dark themes are just two token sets. No component library to restyle. |
| Icons | `lucide` (framework-agnostic icon data) rendered by one `IconComponent` | One consistent icon family; no Angular-version peer dependency. |
| Markdown | `marked` + `DOMPurify` | Render on the client, then sanitize; Angular's own sanitizer runs on top. |
| Forms | Angular typed Reactive Forms | Stable, typed, and familiar. Server validation is authoritative. |
| State | Signals in small feature stores/services | Enough for a single-user app; no NgRx. |
| Backend tests | xUnit v3, `WebApplicationFactory`, real SQLite files | Integration tests hit the real database, not mocks. |
| E2E tests | Playwright (Chromium) | The three critical flows from the spec. |
| Packaging | One Docker image (Angular built into the API's `wwwroot`) + Docker Compose | One process, one origin (no CORS), one volume for the database. |
| HTTPS on a VPS | Caddy reverse proxy (automatic Let's Encrypt) | Smallest config for automatic HTTPS. |

## 2. Architecture

A **modular monolith**: one ASP.NET Core process serves the JSON API under `/api` and the compiled
Angular app for every other path.

```
Browser (desktop / phone)
   │  HTTPS (Caddy on a VPS, or Tailscale / localhost)
   ▼
ASP.NET Core (JaCrm.Api)
   ├── /api/*        Minimal API endpoint groups, one per feature module
   ├── /healthz      liveness for Docker / the proxy
   └── /*            Angular SPA (static files + index.html fallback)
   │
   ▼
SQLite file on a mounted volume (/data/jacrm.db)
```

### Backend layout (`src/JaCrm.Api`)

```
Domain/        Entities, enums, value objects and pure business rules (no ASP.NET, no EF queries)
               - ApplicationStatuses   status → phase map, auto submission-date rule
               - SalaryNormalizer      amount → annual gross in base currency, comparability flags
               - CompanyMatching       name/domain normalization for duplicate warnings
               - DashboardCalculator   metric definitions over plain records
Data/          AppDbContext, entity configurations, migrations, dev seeding
Features/      One folder per module: Endpoints (HTTP mapping) + Service (use cases) + Contracts (DTOs)
               Auth, Meta, Companies, Vacancies, Applications, Technologies, Dashboard,
               Search, Settings, DataTransfer
Common/        Error handling (ProblemDetails), validation helpers, paging, clock
Cli/           `hash-password` and `seed` commands (explicit, never automatic)
```

Rules that keep it a *modular* monolith:

- A feature's endpoints call only that feature's service. Services may read other modules' entities
  through `AppDbContext`, but business rules live in `Domain/` and are called from one place.
- Business rules that must be unit-tested (status transitions, salary normalization, duplicate
  matching, dashboard metrics) are pure functions/methods in `Domain/`.
- New modules (contacts, interviews, reminders) are added as a new `Features/<Module>` folder, new
  entities and a migration, without touching existing modules.

### Frontend layout (`web/src/app`)

```
core/       HTTP API clients, auth guard + interceptor, meta (enums/labels from the API),
            settings + theme, keyboard shortcuts, toasts, error handling
shared/     UI building blocks: icon, status badge, empty state, markdown view, confirm dialog,
            salary display/input, tag input, page header, skeletons; app-date pipe
layout/     App shell (sidebar / mobile tab bar), command palette, shortcuts help overlay
features/   dashboard, applications (board, table, detail, forms), companies, vacancies,
            settings, login
```

### Request flow and cross-cutting concerns

- **Auth:** cookie authentication. `POST /api/auth/login` checks the password against the PBKDF2
  hash from `Auth__PasswordHash`, then issues an HTTP-only, `SameSite=Strict`, `Secure` cookie.
  Every `/api` endpoint except login, logout, session check and `/healthz` requires it.
  The login endpoint is rate limited per client IP.
- **CSRF:** `SameSite=Strict` cookie plus a required custom header (`X-Requested-With`) on every
  unsafe `/api` request. Cross-site pages cannot send custom headers without CORS, and CORS is off.
- **Errors:** every failure is an RFC 7807 `ProblemDetails`. Validation failures are
  `400` with a field → messages map; rule violations (delete a referenced company) are `409`.
- **Validation:** request DTOs are validated on the server in the service layer; the Angular
  forms mirror the important rules for fast feedback only.
- **Transactions:** each use case ends in exactly one `SaveChanges`, which SQLite runs in one
  transaction. Status change + history record and the full import use this.
- **Time:** `TimeProvider` everywhere. Timestamps are stored in UTC. "Today" for date-only rules
  (auto submission date, deadlines, staleness) is computed in the configured time zone
  (`App__TimeZone`, default `Europe/Belgrade`).

## 3. Database model

```
Company 1 ──── * Vacancy 1 ──── 0..1 Application 1 ──── * ApplicationStatusHistory
                   │  *                                   (cascade from Application only)
                   ├──── * VacancyRequiredTechnology * ──┐
                   └──── * VacancyPreferredTechnology * ─┴── Technology
AppSettings (single row, Id = 1)      ExchangeRate (one row per currency)
```

| Entity | Key columns and constraints |
|---|---|
| `Companies` | `Name` (required), `NormalizedName` (indexed), `WebsiteDomain` (indexed), size enum, rating 1–5 (check constraint), notes, `IsArchived` + `ArchivedAt`, `CreatedAt`, `UpdatedAt` |
| `Vacancies` | `CompanyId` FK **Restrict**, `Title` (required), seniority, Markdown description, URL, source + detail, country/city, work arrangement, remote eligibility + `OpenToMyLocation` (Yes/No/Unknown), relocation (Yes/No/Unknown + notes), employment type, experience (years + text), posted salary range (owned type), `DateDiscovered` and `ApplicationDeadline` (`DateOnly`), notes, archive flag, timestamps |
| `Applications` | `VacancyId` FK **Restrict** + **unique index** (one application per vacancy), status, priority, archive flag + date, `SubmittedOn` (`DateOnly`), `StatusChangedAt`, notes, reason, expected/offered salary (owned types), compensation notes, CV and cover-letter references, `PostingSnapshot` (JSON text, immutable), timestamps |
| `ApplicationStatusHistory` | `ApplicationId` FK **Cascade**, `FromStatus` (null for the initial status), `ToStatus`, `ChangedAt`, `Note`; index on `(ApplicationId, ChangedAt)` and on `ChangedAt` |
| `Technologies` | `Name`, `NormalizedName` (**unique**) |
| `VacancyRequiredTechnologies`, `VacancyPreferredTechnologies` | composite PK `(VacancyId, TechnologyId)`, cascade from vacancy, restrict from technology |
| `AppSettings` | base currency, working hours per month, stale threshold days, date format, theme |
| `ExchangeRates` | `Currency` (**unique**, ISO 4217), `RateToBase` (decimal), `UpdatedOn` |

Money is `decimal` everywhere (SQLite stores it as TEXT, so precision is exact). A salary value is
modelled once as `SalaryValue` (amount + currency + period + type) and a posted range as
`SalaryRange` (min + max + the same terms); both are EF Core owned types. Enums are stored as
strings so the database stays readable and reordering an enum cannot corrupt data.

## 4. Main screens

| Route | Screen |
|---|---|
| `/login` | Password-only sign in |
| `/` | Dashboard: six metrics, pipeline by phase/status, stale applications, recent activity, upcoming deadlines, quick actions |
| `/applications` | Kanban board ⇄ table (toggle with `V`), filters, search, archived filter |
| `/applications/:id` | One page: status + key facts, compensation (with normalized comparison), posting snapshot, submission details, notes, status timeline |
| `/applications/:id/edit` | Edit the pursuit fields |
| `/companies`, `/companies/:id` | Company table; detail with vacancies, applications, cross-application status history, notes |
| `/vacancies`, `/vacancies/new`, `/vacancies/:id`, `/vacancies/:id/edit` | Vacancy table with filters; full form for pasting postings; detail with "Convert to application" |
| `/settings` | Theme, base currency, exchange rates, working hours, stale threshold, date format, export/import |
| Overlays | Command palette (`Ctrl/⌘ K`), shortcuts help (`?`), create dialogs, confirm dialogs |

## 5. Milestones

| Phase | Deliverable | Commit |
|---|---|---|
| 1 | This plan + `decisions.md` | `Phase 1: technical plan` |
| 2 | Solution, database + first migration, auth, design system, shell + command palette, error handling, Docker Compose | `Phase 2: project foundation` |
| 3 | Companies, vacancies, applications, status history, settings, dashboard, search/filtering, export/import, with unit + integration tests | `Phase 3: core functionality` |
| 4 | UX polish: forms, tables, Kanban keyboard flow, palette and shortcuts, responsive, states | `Phase 4: UX refinement` |
| 5 | All tests green incl. Playwright E2E; persistence across restarts/rebuilds; export → import round-trip | `Phase 5: quality assurance` |
| 6 | `docs/REPORT.md`, README, AI-agent docs | `Phase 6: final report` |
