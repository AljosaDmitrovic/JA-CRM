# Build a Personal Job Application CRM

## 1. Your role

Act as a senior software architect, senior full-stack engineer, and senior product/UI designer.

You are responsible for making the right technical and architectural decisions, not simply following instructions mechanically.

Think critically about:
- Architecture and maintainability.
- Database design and data integrity.
- Performance and scalability.
- Security and privacy.
- User experience.
- Simplicity and long-term maintainability.

I want a genuinely useful, polished application that I can use every day to manage my personal job search.

Do not overengineer it. Prefer simple, well-structured solutions over unnecessary abstractions.

## 2. Product vision

Build a personal Job Application CRM.

Context: I'm a software developer based in Serbia, applying for roles abroad and for remote roles. I regularly compare opportunities across countries and currencies (e.g. EUR, CHF, RSD), across pay periods, and between working remotely from Serbia and relocating. The application should make those comparisons easy.

The application will allow me to manage my entire job search in one place.

I want to track:
- Companies I'm interested in.
- Job vacancies I've discovered.
- Applications I've submitted.
- Application statuses and progress.
- Salary expectations and compensation.
- Original job descriptions and requirements.
- Recruiters and company contacts (future functionality).
- Interviews and communication history (future functionality).
- Follow-ups and reminders (future functionality).

Think of a lightweight, personal version of Salesforce designed specifically for managing a software developer's job search.

This is an independent application. It must not depend on another application to function.

## 3. Technology and deployment

### 3.1 Stack

I'm a .NET + Angular developer and will maintain this application myself.

- Prefer **ASP.NET Core (Web API)**, **EF Core** and **Angular** unless there's a strong reason otherwise. Justify any deviation in the decisions log (see Phase 1).
- Use **SQLite** as the database: a single file, trivial to back up, no database server to operate.
- Choose the remaining pieces yourself: UI component approach, styling solution, form validation, state management, and testing frameworks.
- If you use a component library, restyle it to match the design system in section 4. The result must not look like a default admin template.

Optimize for:
1. Developer productivity.
2. Maintainability.
3. Excellent user experience.
4. Reliability.
5. Ease of deployment.
6. Minimal operational complexity.

Use a modular monolith with clear separation of concerns. Avoid microservices; this application does not require distributed services.

Document the chosen stack and briefly explain why it is appropriate.

Do not wait for my approval. Make reasonable decisions, record them, and proceed with implementation.

### 3.2 Deployment

I will use the application on my desktop and on my phone, so it must be self-hostable.

- It runs locally with a single command (Docker Compose).
- It can be deployed to a small VPS behind an HTTPS reverse proxy. Document how.
- The SQLite database file lives on a mounted volume so data survives container restarts and rebuilds.

### 3.3 Authentication

Single-user authentication is always on:
- One account. The password hash is supplied via an environment variable.
- Provide a simple command to generate that hash.
- Secure, HTTP-only, SameSite session cookie.
- Rate limiting on the login endpoint.
- No registration, no password-reset flows, no multiple users.

## 4. Design philosophy

Act as a senior product designer.

I strongly prefer minimal, modern, elegant interfaces.

The application should feel like a premium productivity product rather than a generic enterprise CRM.

Design references:
- Linear
- Notion
- Raycast
- Stripe Dashboard

Design principles:

- Minimalist interface.
- Clean typography.
- Generous whitespace.
- Excellent visual hierarchy.
- Neutral color palette.
- Restrained use of accent colors.
- Subtle borders and separators.
- Consistent spacing.
- Carefully designed empty states.
- Beautiful tables and forms.
- Intuitive navigation.
- Fast interactions.
- Consistent iconography.

Avoid:
- Excessive cards.
- Unnecessary borders.
- Excessive rounded corners.
- Strong gradients.
- Glassmorphism.
- Excessive animations.
- Oversized typography.
- Unnecessary decorative elements.
- Cluttered dashboards.
- Generic-looking admin templates.

Use colors strategically. Status colors should communicate meaning rather than decorate the interface.

The application should be desktop-first but fully responsive on mobile.

Support light and dark themes (plus "follow system").

Accessibility, keyboard navigation, and readable contrast are important. Specifically:

- **Command palette** (Ctrl/Cmd+K) to search and jump to any company, vacancy or application, and to run quick actions (add company, add vacancy, create application).
- **Keyboard shortcuts** for common actions (e.g. create a record, switch between Kanban and table view), with an in-app shortcuts help overlay.
- **Every drag-and-drop interaction has a keyboard-accessible alternative** (e.g. a "Move to…" menu on each Kanban card).

## 5. Core application structure

Create the following main navigation:

1. Dashboard
2. Applications
3. Companies
4. Vacancies
5. Settings

The navigation should be minimal and intuitive.

### 5.1 Dashboard

The dashboard is the application's home screen.

It should immediately communicate the current state of my job search.

**Key metrics** (use exactly these definitions):
- **Total applications:** every application, archived included.
- **Active applications:** status is Applied, any interview status, or Offer Received; not archived.
- **Interviews in progress:** status is Recruiter Interview, Technical Interview, Hiring Manager Interview or Final Interview; not archived.
- **Offers received:** applications that have reached Offer Received at any point (derived from status history), archived included.
- **Rejected applications:** current status is Rejected, archived included.
- **Awaiting response:** current status is Applied; not archived. Of these, applications with no status change for at least N days (N is set in Settings, default 14) are highlighted as stale. Stale applications are the candidates for manually moving to No Response.

**Application pipeline**
A visual overview of non-archived applications grouped by phase and status (see 5.2).

**Recent activity**
Recently created or updated applications and recent status changes. Build this from status history and created/updated timestamps. Do not build a separate field-level audit log.

**Upcoming deadlines**
Vacancies with an application deadline in the next 30 days whose application has not yet been submitted (no application, or an application still in a Pipeline status).

**Quick actions**
- Add company.
- Add vacancy.
- Create application.

Keep the dashboard informative without overwhelming the user.

All dashboard metrics must be calculated from real database records, not hardcoded demo values.

### 5.2 Applications

This is the most important part of the application.

Implement two ways to visualize applications:

- Kanban board.
- Table/list view.

#### Statuses and phases

Statuses are grouped into phases:

| Phase | Statuses |
|---|---|
| Pipeline | Interested, Researching, Ready to Apply |
| Applied | Applied |
| Interviewing | Recruiter Interview, Technical Interview, Hiring Manager Interview, Final Interview |
| Offer | Offer Received |
| Closed | Rejected, Withdrawn, No Response |

**Archived is not a status.** It is a separate flag (with an archived date) on the application:
- An archived application keeps its last status, so archiving a rejected application does not erase the fact that it was rejected.
- Archived applications are hidden from the Kanban board and from default lists, are reachable via a filter, and can be unarchived.

#### Kanban board

- One column per status, visually grouped by phase.
- Closed-phase columns are collapsed by default and can be expanded.
- Cards within a column are ordered by priority, then by last updated. Do not persist a manual card order.
- Moving a card to another column changes its status. Every move must be persisted and recorded in status history.
- Each card offers a keyboard-accessible "Move to…" menu as an alternative to drag and drop.

#### Status transition rules

- Any status can move to any other status. Real recruiting processes skip steps and go backwards.
- Every status change creates an `ApplicationStatusHistory` record (from status, to status, changed at, optional note) in the **same transaction** as the status update.
- When an application first enters Applied or any later phase and its submission date is empty, set the submission date automatically. I can edit it afterwards.
- No Response is set manually. The app only highlights stale applications (see Dashboard).

#### Application fields

An application owns everything about **my pursuit** of a vacancy. The posting details belong to the vacancy (see 5.4 and section 6).

**General information**
- Vacancy (and, through it, the company).
- Status.
- Priority (Low / Medium / High).
- Archived flag and archived date.
- Creation date.
- Submission date.
- Last updated date.

**Application details**
- Personal notes (Markdown).
- Reason for applying.

**Posting snapshot (read-only)**
- An immutable snapshot of the vacancy taken when the application is created: job title, company name, job description, required and preferred technologies, required experience, location, work arrangement, remote eligibility, relocation support, employment type, posted salary range, and original URL.
- Store it as a single structured snapshot (e.g. a JSON column). It is never edited, so the application keeps its history even if the vacancy is edited or archived later.

**Compensation**
- Expected salary.
- Offered salary, when applicable.
- Each amount is a full salary value: amount, currency, payment period (hourly / monthly / annual), and salary type (gross / net / unknown).
- Additional compensation notes (e.g. 13th salary, bonus, equity, benefits).

**Submitted application**
- CV version name or reference.
- Cover letter reference or notes.

**Status history**
- A timeline of all status changes.

The application detail page should present all of this on a single, clear page: current status and key facts at the top, then compensation, posting snapshot, submission details, notes, and the status history timeline. Avoid making me navigate through multiple pages to understand the current state of an application.

#### Creating applications

- From a vacancy: one action ("Convert to application"), with a small dialog to pick the initial status (default: Interested).
- Directly ("Create application" quick action): if no vacancy exists yet, the form creates the vacancy inline (minimum: job title and company, with inline company creation) and then creates the application from it.
- A vacancy can have at most one application.

### 5.3 Companies

Create a company database.

Each company has:

- Company name (**the only required field**).
- Website.
- Careers page.
- Industry.
- Headquarters.
- Country.
- City.
- Company size (predefined ranges, e.g. 1–10, 11–50, 51–200, 201–1,000, 1,001–5,000, 5,000+).
- General notes (Markdown).
- Personal rating (1–5).
- Date added.
- Archived flag.

A company can have multiple vacancies.

The company detail page displays:
- Company information.
- All associated vacancies.
- All associated applications.
- Application status history across those applications.
- Personal notes.

**Duplicate detection:** when creating or renaming a company, warn me if a likely duplicate exists. A match is either:
- the same normalized name (case-insensitive, whitespace and punctuation ignored, common legal suffixes ignored, such as Inc, Ltd, LLC, GmbH, AG, SA, B.V., d.o.o.), or
- the same website domain (ignoring `www.` and protocol).

Show the warning with a link to the existing company. I can still proceed. It is a warning, not a hard block.

**Deletion:** a company with linked vacancies or applications is archived, not deleted. Hard delete is only possible when nothing references it.

### 5.4 Vacancies

Create a dedicated vacancy management module. A vacancy owns everything about **the posting**.

Each vacancy has:

- Job title (**required**).
- Company (**required**; can be created inline from the vacancy form).
- Seniority (Intern, Junior, Mid, Senior, Lead, Principal).
- Job description (Markdown, so pasted postings keep their structure).
- Original vacancy URL.
- Source (LinkedIn, Company website, Referral, Recruiter, Job board, Other) plus an optional free-text detail.
- Location: country and city.
- Work arrangement (Remote / Hybrid / On-site).
- Remote eligibility: where the role may be performed from (free text, e.g. "EU only", "Worldwide", "CET ±2h"), plus "Open to my location": Yes / No / Unknown.
- Relocation support: Yes / No / Unknown, plus notes.
- Employment type (Full-time employee, Part-time employee, Contractor / B2B, Freelance, Internship).
- Required technologies (tags).
- Preferred technologies (tags).
- Required experience (optional years, plus free text).
- Posted salary range: minimum, maximum, currency, payment period, salary type.
- Date discovered.
- Application deadline.
- Notes (Markdown).
- Archived flag.

Vacancies are searchable and filterable.

A vacancy can exist without an application.

I can convert a vacancy into an application with one action (see 5.2). The conversion preserves the original vacancy information through the posting snapshot.

**Deletion:** a vacancy with an application is archived, not deleted. Hard delete is only possible when no application references it.

### 5.5 Search and filtering

Implement useful search and filtering throughout the application.

I should be able to search by:
- Company name.
- Job title.
- Technology.
- Location.
- Notes.

Filters include:
- Application status and phase.
- Archived (hidden by default).
- Company.
- Country.
- Work arrangement.
- Open to my location.
- Seniority.
- Employment type.
- Technology tags.
- Date range.
- Salary range (compared on the normalized annual gross value in the base currency, see 5.6 and 6).

Provide sorting options for relevant fields.

The command palette (section 4) provides global search across all entities.

Search and filtering should feel fast and intuitive.

### 5.6 Settings

Settings contains:

- **Theme:** light / dark / system.
- **Base currency** used for compensation comparisons (default EUR).
- **Exchange rates:** a manually maintained list of rates from each currency I use to the base currency, each with a "last updated" date. No external exchange-rate API.
- **Working hours per month** used to convert hourly rates (default 168). Monthly amounts are converted to annual by multiplying by 12.
- **Stale threshold:** N days without a status change before an Applied application is highlighted (default 14).
- **Date display format** (default `dd.MM.yyyy`, with ISO `yyyy-MM-dd` as an option).
- **Data:** export (full JSON, plus CSV per entity) and import/restore from a JSON export (see section 7).

## 6. Data model and architecture

Design a proper relational data model.

Entities:

- `Company`
- `Vacancy`
- `Application`
- `ApplicationStatusHistory`
- `Technology` (tags), linked to vacancies via join tables for required and preferred technologies
- `Settings` (single row) and `ExchangeRate`

Establish appropriate relationships, constraints, indexes, and foreign keys.

**Field ownership** (this prevents duplicated data):

- **Vacancy owns the posting:** title, description, requirements, technologies, location, work arrangement, remote eligibility, relocation support, employment type, posted salary range, URL, source.
- **Application owns my pursuit:** status, priority, dates, expected and offered salary, CV and cover-letter references, reason for applying, notes.
- On creation, the application stores an **immutable snapshot of the posting** (see 5.2).
- The application's company is **derived through its vacancy**, not stored twice.

Domain rules:

- One company can have multiple vacancies.
- One vacancy can have at most one application (enforced by a unique constraint).
- An application must reference a valid vacancy.
- Companies and vacancies with linked records are archived, not deleted. Enforce this with restrictive foreign keys **and** in the service layer. Deleting a company must never cascade into application history.
- Status changes are auditable: the status update and its history record are written in the same transaction.
- Money is stored as a decimal type, never floating point. Every amount carries a currency (ISO 4217 code), a payment period (hourly / monthly / annual) and a salary type (gross / net / unknown). Model this once as a reusable salary value (e.g. an EF Core owned type).
- **Compensation comparison:** normalize amounts to annual gross in the base currency using the exchange rates and working hours from Settings. Net and unknown amounts are shown but clearly flagged as not directly comparable. Do not estimate taxes.
- **Dates:** timestamps are stored in UTC and displayed in local time. Date-only values (date discovered, application deadline, submission date) are stored as date types so they never shift across time zones.
- Markdown content is sanitized when rendered.
- Historical application data remains available even if a vacancy is edited or archived later (via the snapshot).

Design the architecture so that additional modules can be introduced later without rewriting the core application.

## 7. Functional requirements

The application must support:

- Creating, editing, viewing, archiving and deleting records, following the archive-vs-delete rules above.
- Minimal required fields so records can be captured quickly (company: name; vacancy: title and company).
- Proper form validation.
- Confirmation before destructive actions.
- Duplicate-company warnings.
- Loading states.
- Error handling.
- Empty states.
- Success and error notifications.
- Persistent data storage.
- Search and filtering.
- Sorting.
- Responsive layouts.
- Keyboard alternatives to drag and drop.
- **Export:** a full JSON export (all entities, status history, settings and exchange rates, with a schema version), plus CSV export per entity for spreadsheets.
- **Import/restore:** import a JSON export into the application. Importing replaces all existing data, after an explicit confirmation. Export followed by import must round-trip without data loss.

Implement validation on both client and server. Do not rely exclusively on frontend validation.

## 8. Non-functional requirements

**Security**
- Protect personal career data.
- Never hardcode secrets. Use environment variables for sensitive configuration, and provide an `.env.example`.
- Single-user authentication as described in 3.3.
- Protect against common web application vulnerabilities (XSS, including via Markdown; CSRF where applicable; injection; insecure cookies).

**Performance**
- Avoid unnecessary database queries (and N+1 queries).
- Use pagination or efficient loading for larger lists.
- Avoid unnecessary frontend re-renders and change-detection work.
- Keep the application responsive.

**Reliability**
- Handle database failures gracefully.
- Use proper transaction boundaries.
- Ensure that status changes and their history records remain consistent.
- Document where the database file lives and how to back it up, in addition to the export/import feature.

**Code quality**
- Meaningful naming.
- Clear separation of concerns.
- Reusable components where appropriate.
- Avoid unnecessary abstractions.
- No duplicated business logic.
- No unnecessary dependencies.
- No oversized components or services.
- Maintainable and testable code.

## 9. Testing

Implement automated tests for important business functionality, in three layers:

**Unit tests (domain logic)**
- Status transitions and the automatic submission-date rule.
- Salary normalization (currencies, periods, gross/net/unknown flagging).
- Duplicate-company matching.
- Dashboard metric definitions.

**API integration tests (against a real SQLite database, not mocks)**
- Creating and editing companies, vacancies and applications.
- Archive-vs-delete rules and foreign-key integrity.
- Vacancy-to-application conversion and snapshot immutability.
- Status change and history written in one transaction.
- Search, filtering and sorting.
- Salary validation.
- Authentication on protected endpoints.

**End-to-end tests (e.g. Playwright), for the critical flows**
- Convert a vacancy into an application.
- Move an application between statuses, both by drag and drop and via the keyboard "Move to…" menu.
- Export, then import, with no data loss.

Do not consider the project complete merely because it compiles.

Run the tests and fix discovered issues.

## 10. Development approach

Work in the following order. **Commit to git at the end of each phase** with a clear message, so each phase can be reviewed and rolled back.

**Phase 1 — Repository inspection and technical planning**

This is a new project. The repository is the folder that contains this prompt file and is otherwise empty. Initialize git there.

Create a short technical implementation plan covering:
- Selected technologies.
- Architecture.
- Database model.
- Main application screens.
- Main development milestones.

Save it in `docs/architecture.md`.

Create `docs/decisions.md` and record every significant decision and every assumption you make there. You are working without my approval, so every guess must at least be visible.

**Phase 2 — Project foundation**

Set up:
- Application structure.
- Database.
- Persistence layer.
- Initial migrations.
- Authentication.
- Global styling and design system (including light/dark themes).
- Navigation and command palette shell.
- Error handling.
- Docker Compose setup.

**Phase 3 — Core functionality**

Implement:
1. Companies.
2. Vacancies.
3. Applications.
4. Application statuses and history.
5. Settings (including exchange rates).
6. Dashboard.
7. Search and filtering.
8. Export and import.

**Phase 4 — UX refinement**

Polish:
- Layouts.
- Forms.
- Tables.
- Kanban interactions (including the keyboard alternative).
- Command palette and keyboard shortcuts.
- Responsive behavior.
- Loading and empty states.
- Notifications.
- Error states.

**Phase 5 — Quality assurance**

- Run all automated tests.
- Fix bugs.
- Check database integrity.
- Check responsive layouts.
- Verify that all CRUD and archive operations work.
- Verify that data persists after restarting the application and after rebuilding the containers.
- Verify the export → import round-trip.

**Phase 6 — Final report**

Write `docs/REPORT.md` (and keep `README.md` up to date) covering:
- How to run the application locally.
- How to deploy it to a VPS.
- How to set the password.
- How to back up and restore data.
- Assumptions and any deviations from this prompt.
- Known limitations.

## 11. Development rules

1. Do not implement everything inside one large component or service.
2. Do not introduce unnecessary architectural complexity.
3. Do not use mock data as a substitute for real functionality.
4. Do not leave placeholder buttons or nonfunctional interactions.
5. Do not invent features that are outside the defined scope.
6. Do not sacrifice usability for technical sophistication.
7. Do not overcomplicate the database model.
8. Do not leave unfinished TODOs in core functionality.
9. Prefer incremental, testable implementation.
10. Keep documentation updated when architectural decisions change.
11. Record every assumption in `docs/decisions.md`.

Use realistic seed data only for development and demonstration. Seeding runs through a separate, explicit command and must never run automatically against my real database.

## 12. Scope boundaries

This is a private, single-user productivity application.

Do not implement:
- Multi-tenancy.
- Multiple users.
- Subscription management.
- Billing.
- Team collaboration.
- Enterprise permissions.
- Social features.
- AI integrations.
- Exchange-rate APIs or other complicated third-party integrations.

The following are intentionally reserved for future development:

- Recruiter and contact management.
- Interview scheduling and interview notes.
- Follow-up automation and reminders.
- Calendar integration.
- Email integration.
- Advanced salary and career analytics.
- CV Manager integration.

Keep the architecture extensible, but do not build these features prematurely.

## 13. Definition of success

The finished application should allow me to:

1. Add a company in seconds.
2. Save a vacancy with its complete job description.
3. Convert a vacancy into an application.
4. Move an application through the recruitment pipeline, with the mouse or the keyboard.
5. Quickly understand the state of my entire job search.
6. Compare opportunities and compensation across currencies, pay periods and work arrangements.
7. Find any company, vacancy, or application quickly.
8. Preserve my complete application history.
9. Export, back up and restore my personal data.

The application should feel polished enough that I would genuinely want to use it every day.

**Final instruction:**

Think like an experienced software engineer building a real product for a demanding user.

Make sensible technical decisions independently, record them, prioritize excellent UX, and implement working functionality rather than simply generating a project structure.

Start by inspecting the repository, confirming the architecture, and then proceed with implementation.
