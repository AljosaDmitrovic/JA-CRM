# Deployment, backup and restore

The app is one container (`app`) that serves the API and the web UI on port 8080. All state lives
in one folder, mounted at `/data` from the Docker volume `jacrm-data`:

```
/data/jacrm.db            SQLite database (with -wal/-shm files while running)
/data/keys/               keys that encrypt the session cookie (keep them, or everyone is signed out)
/data/backups/            automatic and manual database copies
```

## Settings (`.env`)

| Variable | Meaning |
|---|---|
| `JACRM_PASSWORD_HASH` | **Required.** Output of `hash-password` (see below). |
| `JACRM_PORT` | Local port, default 8080 (local Compose only). |
| `JACRM_SECURE_COOKIE` | `false` for plain-HTTP use on localhost/LAN; the VPS overlay forces `true`. |
| `JACRM_TIME_ZONE` | IANA zone for "today" (deadlines, staleness, submission dates). Default `Europe/Belgrade`. |
| `JACRM_DOMAIN` | VPS only: the domain Caddy gets a certificate for. |

## Password

```bash
docker compose run --rm app hash-password                         # interactive, hidden input
echo 'my long passphrase' | docker compose run --rm -T app hash-password   # non-interactive
```

Put the printed value in `.env` as `JACRM_PASSWORD_HASH`, then `docker compose up -d`. To change
the password, generate a new hash, replace it, and restart. Existing sessions stay valid until
they expire (30 days, sliding), or until you sign out. To sign out everywhere, delete
`/data/keys` and restart. Login is limited to 5 attempts per minute per IP.

## Option A: a small VPS with HTTPS (recommended for laptop + phone)

Needs a Linux VPS with Docker, a domain or subdomain, and DNS `A`/`AAAA` records pointing at the server.

```bash
git clone git@github.com:AljosaDmitrovic/JA-CRM.git && cd JA-CRM
cp .env.example .env            # set JACRM_DOMAIN=jobs.example.com and JACRM_PASSWORD_HASH
docker compose -f compose.yaml -f compose.vps.yaml up -d --build
```

Caddy (`deploy/Caddyfile`) gets and renews the Let's Encrypt certificate and is the only public
service (ports 80/443). The app port is not published, and the app trusts the proxy's
`X-Forwarded-*` headers, so the login rate limit sees real client IPs. HSTS is on.

Firewall: allow 22, 80 and 443 only.

## Option B: your own machine + Tailscale (no domain, nothing public)

Run the local Compose setup on a machine that stays on, install Tailscale on it and on your
phone, then:

```bash
tailscale serve --bg 8080       # https://<machine>.<tailnet>.ts.net with an automatic certificate
```

Keep `JACRM_SECURE_COOKIE=true` when you use `tailscale serve` (the page is HTTPS). If you
instead open `http://<machine>:8080` over the tailnet, set it to `false`. WireGuard still
encrypts the traffic.

## Updating

```bash
git pull
docker compose up -d --build                     # or with -f compose.vps.yaml on the VPS
```

Database migrations run at startup. When the database already has data, a copy is first written
to `/data/backups/jacrm-<timestamp>-pre-migration.db`.

## Backup and restore

**JSON export (portable, human-readable).** *Settings → Data → Export JSON* downloads everything,
including the status history, settings and exchange rates, with a schema version. Restore it with
*Settings → Data → Import JSON…*. Importing replaces all data. Before it does, it validates
the whole file and writes `/data/backups/jacrm-<timestamp>-pre-import.db`.

**Database copy (exact).** These run while the app is up (SQLite `VACUUM INTO` gives a consistent copy):

```bash
docker compose exec app dotnet JaCrm.Api.dll backup
docker compose cp app:/data/backups ./backups
```

Restore a database copy:

```bash
docker compose stop app
docker compose run --rm -v "$PWD/backups:/restore:ro" --entrypoint sh app -c \
  'rm -f /data/jacrm.db-wal /data/jacrm.db-shm && cp /restore/jacrm-YYYYMMDD-HHMMSS-manual.db /data/jacrm.db'
docker compose start app
```

Old files in `/data/backups` are never deleted automatically. Prune them now and then.

Automate offsite copies with a nightly cron job on the VPS: run `backup`, then copy the newest file
somewhere else (another machine, object storage).

## Without Docker

```bash
cd web && npm ci && npx ng build && cd ..
dotnet publish src/JaCrm.Api -c Release -o out
cp -r web/dist/web/browser out/wwwroot
App__DataDirectory=/var/lib/jacrm Auth__PasswordHash='…' ASPNETCORE_URLS=http://127.0.0.1:8080 dotnet out/JaCrm.Api.dll
```

Put it behind any HTTPS reverse proxy, and set `App__TrustForwardedHeaders=true` only when the
app port is reachable from the proxy alone.
