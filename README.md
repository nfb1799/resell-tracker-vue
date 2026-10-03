# Resell Tracker (Vue + ASP.NET Core)

A port of [Resell Tracker](https://github.com/nfb1799/resell-tracker) (React + Firebase)
to a Vue 3 + TypeScript front end and an ASP.NET Core API on SQL Server. Work in
progress; see [PARITY.md](PARITY.md) for what has been carried over so far.

## Layout

| Path | What |
|---|---|
| `client/` | Vue 3, TypeScript (strict), Pinia, Vue Router, Vite, Vitest |
| `server/` | ASP.NET Core Web API (.NET 10), xUnit |
| `shared/` | Test fixtures both halves run against |
| `docker-compose.yml` | Optional local SQL Server for machines with Docker |

## Running locally

Requires Node 24, the .NET 10 SDK and SQL Server 2019 or later. On Windows the
default connection string points at a local **SQL Server Express** instance
(`.\SQLEXPRESS`, Windows auth), so no Docker is needed; LocalDB works too.
Elsewhere, or if you prefer a container:

```bash
cp .env.example .env          # then set a SQL Server password
docker compose up -d
```

and point `ConnectionStrings__Default` at `localhost,1433`.

```bash
cd server && dotnet run --project src/ResellTracker.Api   # API on :5086
cd client && npm install && npm run dev                   # SPA on :5175, /api proxied
```

The development app applies migrations on startup. To add one, from `server/`
(after `dotnet tool restore`):

```bash
dotnet ef migrations add <Name> --project src/ResellTracker.Api --output-dir Data/Migrations
```

## API

Accounts are ASP.NET Core Identity with a session cookie that is HttpOnly, Secure
and SameSite=Strict. The SPA is served from the same origin, so the cookie is
first-party and never readable from script, and Strict keeps it off every
cross-site request. Sign-in, sign-up, password reset and demo creation are rate
limited per client, and five wrong passwords lock an account for five minutes.

| Endpoint | |
|---|---|
| `POST /api/auth/register` `login` `logout` | Email and password (6+ characters, as in the original) |
| `GET /api/auth/me` | Who is signed in; a demo account says when it expires |
| `POST /api/auth/demo` | A private demo account seeded from `shared/demo-items.json`, deleted after 24 hours |
| `POST /api/auth/forgot-password` `reset-password` | Emailed reset link (Resend). Without an API key, development writes the link to the log |

Everything else needs a signed-in user and only ever sees that user's data.
Errors are [ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457).

| Endpoint | |
|---|---|
| `GET /api/items?status=&platform=&q=` | The user's items, newest first, with thumbnails and computed profit |
| `POST /api/items` | Create (the client may supply the id) |
| `GET PUT DELETE /api/items/{id}` | Read, replace, delete |
| `PUT DELETE /api/items/{id}/sale` | Log or edit a sale; undo it |
| `PUT DELETE /api/items/{id}/donation` | Log or edit a donation; undo it |
| `PUT GET DELETE /api/items/{id}/photo` | Set (thumbnail + full JPEG, multipart), fetch the full image, remove |
| `GET PUT /api/settings` | Display name, currency, theme, monthly goal |
| `GET PUT /api/settings/fees` | Fee schedule per platform |
| `GET /api/stats/dashboard` `/trends` | Overview tiles and trends, computed on the server |
| `POST /api/items/import` | Bulk import: every row checked again, good rows added, bad ones reported |
| `GET /api/export/csv` `/json` | CSV with the full profit breakdown; JSON backup that imports back in |

Changes to an existing item carry its version in `If-Match`; every item response
includes it, and it is also the `ETag`. A stale version gets **412**, a missing one
**428**, and a change the item's status doesn't allow (selling a donated item) **409**.

## Offline and installing

The client is a PWA: installable from the browser, and usable with no connection.

- **Opening offline.** The service worker precaches the app shell. The API is
  deliberately kept out of it; instead the app saves the server's last answer
  (items, settings, stats, who is signed in) in IndexedDB, per user, and opens
  from that when the server can't be reached.
- **Changes offline.** Each change joins an outbox in IndexedDB and shows at once,
  worked out with the same TypeScript rules the forms use. When the server is
  reachable again (the browser's online event, or a health check every 15
  seconds while offline) the outbox is sent in order.
- **Conflicts.** Every change carries the item version it was made against. If
  the item changed elsewhere meanwhile, the server answers 412 and the change is
  held under "needs your attention", showing each field it changed next to the
  server's value: keep mine (only the fields I changed, on top of the latest) or
  keep theirs. New items carry ids made on the device, so replaying a create the
  server already has is harmless, and a change the server turns out to have
  already is treated as done.

Lighthouse dropped its PWA audit in version 12, so `npm run test:e2e` asks the
browser directly (`Page.getInstallabilityErrors`) using the Chrome or Edge
already installed; `BASE_URL=https://… npm run test:e2e` checks a deployed site.

## Deployment

One container: the API serving the built SPA from `wwwroot` (see the
`Dockerfile`), on **Azure Container Apps**, with **Azure SQL Database** on the free
offer. Both scale to zero when idle, so the first visit after a quiet spell takes
a few seconds while the container starts and the database resumes; the API
retries the database's "still waking up" errors rather than failing.

- **CI/CD.** Every push builds the image. On `main` it is pushed to GitHub's
  container registry, rolled out with `az containerapp update`, and the live site
  is checked: it answers, and Chrome finds it installable and usable offline.
  GitHub signs in to Azure with OIDC, so no Azure password is stored anywhere.
- **Configuration**, set by `infra/azure-setup.sh` as Container Apps settings and
  secrets: `ConnectionStrings__Default`, `Email__ResendApiKey`, `App__BaseUrl`
  (where reset links point), `Database__MigrateOnStartup`, and
  `Proxy__TrustForwardedHeaders` (client IPs for rate limiting, behind the proxy).
- **Sessions survive restarts.** The keys that encrypt the sign-in cookie are kept
  in the database rather than the container.
- **Email.** Password-reset links go out through Resend. Without a verified domain
  Resend only delivers to the account owner's address; adding a domain and setting
  `Email__From` lifts that with no code change.

To set it up from nothing: push to `main` once so CI publishes the image, make the
package public on GitHub, run `infra/azure-setup.sh` in Azure Cloud Shell, and add
the repository variables it prints. Every later push to `main` deploys itself.

## Checks

```bash
cd client && npm run lint && npm test && npm run build && npm run test:e2e
cd server && dotnet format --verify-no-changes && dotnet build && dotnet test
```

The server's integration tests run against a real SQL Server: each run creates a
throwaway database through the migrations and drops it afterwards. Locally that is
`.\SQLEXPRESS`; set `RESELLTRACKER_TEST_SQL` to a connection string without a
database name to use another server.

CI runs the same on every push and pull request, with SQL Server in a service
container, and also fails if the EF model has changes no migration covers.
