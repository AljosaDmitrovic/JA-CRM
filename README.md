# Job Search CRM

A personal, single-user CRM for a software developer's job search. It tracks companies, saves
vacancies with their full job descriptions, and moves applications through a Kanban pipeline.
It compares salaries across currencies and pay periods, and exports everything for backup.

ASP.NET Core 10 + EF Core + SQLite, Angular 22, one Docker image.

## Quick start (Docker)

```bash
cp .env.example .env
docker compose run --rm app hash-password      # type your password twice, copy the hash
# put the hash in .env:  JACRM_PASSWORD_HASH=AQAAAA…
docker compose up -d --build
open http://localhost:8080
```

Your data lives in the Docker volume `jacrm-data` (the SQLite database, session keys and
automatic backups). Restarting or rebuilding the container keeps it.

To use the app from your phone too, deploy it to a small VPS behind HTTPS, or reach your home
machine over Tailscale. Both are described in [docs/deployment.md](docs/deployment.md). On the
phone, use *Add to Home Screen* for an app-like icon.

## Development

Requirements: .NET SDK 10.0.400+, Node 22, (optional) Docker.

```bash
# API on http://localhost:5080 (data in src/JaCrm.Api/App_Data)
HASH=$(echo 'dev-password' | dotnet run --project src/JaCrm.Api --no-launch-profile -- hash-password)
Auth__PasswordHash="$HASH" dotnet run --project src/JaCrm.Api

# Optional demo data (only into an empty database)
Auth__PasswordHash="$HASH" dotnet run --project src/JaCrm.Api -- seed

# Angular on http://localhost:4200 (proxies /api to :5080)
cd web && npm ci && npm start
```

## Tests

```bash
dotnet test --solution JaCrm.slnx                 # 177 unit + integration tests (real SQLite files)
cd web && npx ng build && cd ../e2e && npm ci && npm test   # 3 Playwright end-to-end flows
```

## Backups

- **In the app:** *Settings → Data → Export JSON* creates a full export, which *Import* restores.
  There are CSV exports per record type for spreadsheets.
- **Database file:** `docker compose exec app dotnet JaCrm.Api.dll backup` writes a consistent
  copy to `/data/backups/`. Copy it out with `docker compose cp app:/data/backups ./backups`.
- **Automatic:** a copy is written before every database migration and before every import.

Restoring is covered in [docs/deployment.md](docs/deployment.md#backup-and-restore).

## Using it

- **⌘K / Ctrl+K** opens the command palette (search everything, run actions), and **?** lists every shortcut.
- **N then C / V / A** adds a company, adds a vacancy, or creates an application.
- On the board, drag cards between columns, or focus a card and press **M** to move it with the keyboard.
- Salaries are compared as annual gross in your base currency, using the exchange rates you
  maintain in Settings. Net or unknown amounts are flagged, never estimated.

## Documentation

| File | Contents |
|---|---|
| [docs/REPORT.md](docs/REPORT.md) | Final report: running, deployment, password, backups, assumptions, limitations |
| [docs/deployment.md](docs/deployment.md) | VPS + Caddy (HTTPS), Tailscale, updates, backup and restore |
| [docs/architecture.md](docs/architecture.md) | Stack, structure, data model, screens |
| [docs/decisions.md](docs/decisions.md) | Every decision and assumption (D-01 … D-62) |
| [CLAUDE.md](CLAUDE.md) | Guide for AI coding agents: commands, layout, conventions, progress |
| [job-application-crm-prompt.md](job-application-crm-prompt.md) | The original requirements |
