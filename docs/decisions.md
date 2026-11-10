# Decisions and assumptions

Every significant decision and every assumption made while building the app without the owner's
approval. Newest entries go at the bottom of their section. Format: **what** — *why*.

## Stack and structure

- **D-01 · ASP.NET Core 10 Minimal APIs + EF Core 10 + SQLite + Angular 22.** — The requested
  stack; nothing in the requirements justifies a deviation. Minimal APIs (one endpoint group per
  feature) instead of controllers keep each module in one small folder.
- **D-02 · One backend project (`JaCrm.Api`) with `Domain/`, `Data/`, `Features/` folders, not
  separate class-library projects.** — The app is small. Folders give the same separation; extra
  projects would add ceremony without isolating anything that needs isolating. `Domain/` must not
  depend on ASP.NET Core or run EF queries, so its rules stay unit-testable.
- **D-03 · No mediator, no repository layer.** — Services use `AppDbContext` directly. EF Core is
  already a unit of work and repository; wrapping it adds indirection without value here.
- **D-04 · The API serves the compiled Angular app.** — One container, one origin (no CORS, simpler
  cookies), one port behind the proxy.
- **D-05 · Angular CDK + hand-written SCSS design tokens; no component library.** — The spec forbids
  a default admin look. Restyling Material costs more than writing ~15 small components on top of
  CDK behaviour (drag-drop, dialog, menu, overlay, focus management).
- **D-06 · Icons from the `lucide` package (framework-agnostic data) through one `IconComponent`.**
  — `lucide-angular` declares peer support only up to Angular 21; the plain package has no peer
  dependencies.
- **D-07 · Inter (self-hosted via `@fontsource-variable/inter`).** — The reference products use it;
  self-hosting avoids a third-party font request that would leak usage.
- **D-08 · Signals for state, no NgRx.** — Single user, no shared mutable state worth a store.
- **D-09 · Typed Reactive Forms, not Signal Forms.** — Reactive Forms are stable; Signal Forms were
  still marked experimental when this was built.
- **D-10 · xUnit v3 with plain `Assert`.** — No assertion library needed (FluentAssertions 8 also has
  a commercial licence now).

## Data model

- **D-11 · Integer primary keys.** — Readable URLs (`/applications/42`), and export/import preserves
  them so links survive a restore.
- **D-12 · Enums stored as strings.** — Readable database and CSV; reordering an enum cannot corrupt
  data.
- **D-13 · Two owned value types: `SalaryValue` (amount, currency, period, type) and `SalaryRange`
  (min, max, currency, period, type).** — The spec asks for one reusable salary value; a posted
  range shares the same terms but has two amounts. Both normalize through the same
  `SalaryNormalizer`, so the rule exists once.
- **D-14 · `Application.StatusChangedAt` is stored alongside the history.** — Staleness ("no status
  change for N days") needs it on every dashboard load; it is written in the same `SaveChanges` as
  the history row, so it cannot drift.
- **D-15 · The posting snapshot is a JSON text column (`PostingSnapshot`) written once at creation.**
  — Matches the spec; the C# type is an immutable record and no endpoint can change it.
- **D-16 · Deleting an application is allowed and cascades to its status history only.** —
  The spec restricts deletion of companies and vacancies, not applications; an application's history
  is meaningless without it. Companies/vacancies use `Restrict` FKs so nothing cascades into
  application history.
- **D-17 · Timestamps are `DateTime` stored as UTC with a converter that restores `Kind = Utc` on
  read.** — SQLite has no date type and `DateTimeOffset` cannot be ordered in SQL by the provider.
  Date-only fields use `DateOnly`.
- **D-18 · Company country/city, vacancy country/city and industry are free text with suggestions
  from existing values.** — A full country list adds a dependency and slows entry; suggestions keep
  values consistent enough for filters.
- **D-19 · Currencies are any ISO 4217-shaped code (`^[A-Z]{3}$`).** — Common codes are suggested in
  the UI; an unknown code is allowed but cannot be compared until it has an exchange rate.

## Business rules (interpretations)

- **D-20 · "Applied or any later phase" for the automatic submission date means the Applied,
  Interviewing and Offer phases, not Closed.** — An application can be Withdrawn or Rejected
  straight from the pipeline without ever being submitted; stamping a submission date then would be
  false. The rule also applies when an application is *created* directly in such a status.
- **D-21 · Moving to the current status again is rejected (400).** — It would create a meaningless
  history row.
- **D-22 · Every application gets an initial history row (`From = null → To = initial status`).** —
  Makes "Offers received" (derived from history) and the timeline complete, including applications
  created directly as Offer Received.
- **D-23 · "Today" is computed in a configured time zone (`App__TimeZone`, default
  `Europe/Belgrade`).** — The server runs in UTC in Docker; the owner lives in Serbia.
- **D-24 · Exchange rates are stored as "1 unit of currency = X base currency". Changing the base
  currency re-bases all rates automatically** (requires a rate for the new base currency; the old
  base gets rate `1 / r`). — Otherwise every rate would silently become wrong.
- **D-25 · Salary filter = annual gross in the base currency. Records whose salary is net, unknown
  type, or in a currency without a rate do not match an active salary filter** (they are still shown
  without the filter, flagged as not comparable). — The spec defines the comparison on annual gross
  and forbids tax estimation, so such amounts have no comparable value.
- **D-26 · Which salary represents an application in the salary filter/sort:** the offered salary if
  set, otherwise the vacancy's posted range (from the snapshot). Expected salary is my ask, not the
  opportunity's value, so it is shown but not used for filtering. For ranges, a record matches if
  its range overlaps the filter range.
- **D-27 · Hourly → annual = amount × working hours per month × 12.** — Uses the configured working
  hours (default 168), consistent with "monthly × 12".
- **D-28 · Duplicate company warning also runs on rename and excludes the company being edited.**
- **D-29 · Legal suffixes ignored for matching:** inc, incorporated, ltd, limited, llc, gmbh, ag, sa,
  bv, nv, doo, ad, plc, corp, corporation, co, company, sarl, srl, spa, ab, as, oy, kg, se. —
  The spec's list plus common EU forms the owner is likely to meet. Matching strips punctuation
  first, so `B.V.` and `d.o.o.` become `bv` and `doo`.
- **D-30 · Archived companies/vacancies stay selectable where they are already linked but are hidden
  from pickers for new records.**
- **D-31 · Archiving a company does not archive its vacancies or applications automatically.** —
  Archiving is a filing decision per record; a silent cascade would hide live applications.
- **D-32 · Recent activity = last 12 events merged from: status changes (history), applications
  created, applications edited (`UpdatedAt` not equal to creation or last status change).** — Built
  from timestamps as the spec requires; no audit log.
- **D-33 · Upcoming deadlines include today and the next 30 days, non-archived vacancies only.**

## Security

- **D-34 · Password hash format: ASP.NET Core Identity `PasswordHasher` v3 (PBKDF2-HMAC-SHA512,
  100k iterations), base64.** — Well-reviewed implementation, already in the shared framework, and
  the base64 output contains no `$`, so it is safe in `.env` files (Compose interpolates `$`).
- **D-35 · CSRF defence = `SameSite=Strict` cookie + mandatory `X-Requested-With: JaCrm` header on
  unsafe `/api` requests, CORS disabled.** — Standard OWASP "custom request header" defence; no token
  round-trips needed for a JSON-only API.
- **D-36 · Cookie is `Secure` by default; `Auth__SecureCookie=false` is allowed only for plain-HTTP
  local use.** — Browsers drop `Secure` cookies on plain HTTP origins other than `localhost`.
- **D-37 · Login rate limit: 5 attempts per minute per client IP (fixed window), forwarded headers
  trusted from the reverse proxy only.**
- **D-38 · Session lifetime 30 days, sliding.** — Personal device use (phone); logout is available.
- **D-39 · Markdown is rendered with `marked` and sanitized with DOMPurify; raw HTML in Markdown is
  dropped by the sanitizer and Angular sanitizes `innerHTML` again.**

## Operations

- **D-40 · EF Core migrations run automatically at startup, and a copy of the database file is
  taken first when migrations are pending.** — Single instance, single file: automatic migration is
  safe and removes a manual step; the copy makes a bad migration recoverable.
- **D-41 · Seeding is a CLI command (`seed`) that refuses to run on a non-empty database.** — The
  spec forbids automatic seeding against real data.
- **D-42 · Deployment target for "laptop + phone": a small VPS with Caddy (automatic HTTPS) is the
  primary path; Tailscale on a home machine is documented as the no-domain alternative.**
- **D-43 · A web app manifest (name, icons, theme colour) is included so the app can be added to the
  phone's home screen. No service worker / offline mode.** — Cheap, improves daily phone use;
  offline sync would be out of scope.
- **D-44 · Local (non-Docker) data lives in `src/JaCrm.Api/App_Data/`; Docker uses `/data`.** — A
  folder named `data` would be the same directory as the `Data/` source folder on case-insensitive
  file systems (macOS), mixing the database into the sources and the `.gitignore`.
