# Final report

The application covers the whole specification
([`job-application-crm-prompt.md`](../job-application-crm-prompt.md)). It was built in six
phases, with one commit per phase. Every interpretation of the spec is logged in
[`decisions.md`](decisions.md) (D-01 … D-62), and the structure is in
[`architecture.md`](architecture.md).

## What works

| Area | State |
|---|---|
| Companies | CRUD; only the name is required; duplicate warning by normalized name or domain (a warning, never a block); archive vs delete; detail page with vacancies, applications and their status history |
| Vacancies | Full posting with Markdown description, technology tags, posted salary range, location/remote/relocation; inline company creation; filters, sorting and paging; "Convert to application" |
| Applications | Kanban (one column per status, grouped by phase, Closed collapsed, drag and drop + keyboard "Move to…" with M) and a sortable table; status history in the same transaction; automatic submission date; staleness; immutable posting snapshot; detail and edit pages; undo after a move |
| Dashboard | The six metrics exactly as defined, pipeline by phase and status, stale applications (one click to No Response), recent activity, upcoming deadlines, quick actions |
| Search | ⌘K palette across all records and actions; per-list search and filters (status/phase, company, country, arrangement, open to my location, seniority, employment type, technology, date range, normalized salary) kept in the URL |
| Settings | Theme (light/dark/system), base currency with automatic rate conversion, exchange rates, working hours, stale threshold, date format |
| Data | Full JSON export with schema version, validated import (replace-all, automatic backup first), CSV per record type |
| Security | One password (PBKDF2 hash from env), HTTP-only `SameSite=Strict` cookie, login rate limit, CSRF header check, CSP and security headers, sanitized Markdown, CSV formula-injection protection |
| Operations | One Docker image; Compose for local use and VPS (Caddy, automatic HTTPS); migrations at startup with a backup first; `backup` and `seed` CLI commands |

## Verification

- **177 backend tests** (`dotnet test --solution JaCrm.slnx`), all passing. Unit tests cover the
  domain rules (status phases, submission date, salary normalization, duplicate matching,
  dashboard definitions). Integration tests run against real SQLite files. They cover every
  endpoint's auth, CRUD, archive vs delete including database-level foreign keys, snapshot
  immutability, a forced failure that proves status and history roll back together, search,
  filters, sorting, salary validation, export → import round trip, restart persistence, and
  SQLite `integrity_check` / `foreign_key_check`.
- **3 Playwright flows** (`e2e/`), all passing three runs in a row against the production build:
  vacancy → application conversion, moving a card by drag and drop and by the keyboard menu, and
  export → import through the UI with an identical re-export.
- **Manual checks** with screenshots at 1440 px and 390 px in light and dark: no horizontal page
  scroll, no console errors, overlays (palette, menu, dialogs, shortcuts) keyboard-operable.
- **Docker:** the image builds; Compose starts it healthy (HEALTHCHECK); login, API and SPA deep links
  work; data and the session survive `docker compose restart` and `up --force-recreate` on the volume.

## How to…

- **Run locally:** see the [README](../README.md#quick-start-docker) (`cp .env.example .env`,
  `hash-password`, `docker compose up -d --build`).
- **Deploy to a VPS:** [deployment.md](deployment.md#option-a-a-small-vps-with-https-recommended-for-laptop--phone)
  covers Compose with the Caddy overlay, a domain, and ports 80/443.
- **Set or change the password:** [deployment.md](deployment.md#password).
- **Back up and restore:** [deployment.md](deployment.md#backup-and-restore) covers the JSON export/import, `backup`
  for an exact database copy, and the automatic pre-migration and pre-import copies.

## Main assumptions and interpretations

All of them are in [`decisions.md`](decisions.md). The ones most likely to matter day to day:

- An automatic submission date is set for Applied, Interviewing and Offer, but not for Closed
  (you can be rejected before applying) (D-20).
- The offered salary represents an application in salary filters once known; until then the
  posted range from the snapshot does. The expected salary is your ask and is not used for
  filtering (D-26).
- Net amounts, unknown types and currencies without a rate never match a salary filter (D-25).
- Filters and search use the live vacancy; the snapshot preserves the posting as you applied (D-46).
- Archiving a company does not archive its vacancies or applications (D-31).
- Undo after a move is a second status change, so the history stays auditable (D-55).
- Exchange rates are "1 unit = X base currency"; changing the base currency converts them (D-24).

## Deviations from the prompt

- None in functionality; phases 1 to 6 are implemented and tested.
- The Playwright version is pinned to the one whose browser was already on this machine (D-60).

## Known limitations

- Search uses SQLite `LIKE`, which is case-insensitive for ASCII only: `škoda` does not find `Škoda`.
- Lists filter in SQL but sort and page in memory, because salary sorting depends on exchange rates
  (D-45). This is comfortable for hundreds to a few thousand records, not hundreds of thousands.
- Backups in `/data/backups` are never pruned automatically.
- There is no password-change screen (by design: regenerate the hash), no offline mode, and no
  service worker.
- End-to-end tests run in Chromium only.
- **On the development Mac:** the repository is on the iCloud-synced Desktop. With little free
  disk space, macOS evicts files (including `node_modules`, build output and `.git` objects),
  and builds then hang until iCloud downloads them back. See `CLAUDE.md` for the symptom and the
  fix. A permanent fix is to keep the repository outside iCloud Drive or to free disk space.

## Reserved for later (out of scope by design)

Contacts and recruiters, interviews and notes, reminders and follow-ups, calendar and email
integration, deeper salary analytics, CV manager integration. Each fits as a new
`Features/<Module>` folder plus a migration, without changes to existing modules.
