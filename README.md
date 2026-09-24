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

Every endpoint needs a signed-in user and only ever sees that user's data. Errors
are [ProblemDetails](https://www.rfc-editor.org/rfc/rfc9457).

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

Changes to an existing item carry its version in `If-Match`; every item response
includes it, and it is also the `ETag`. A stale version gets **412**, a missing one
**428**, and a change the item's status doesn't allow (selling a donated item) **409**.

## Checks

```bash
cd client && npm run lint && npm test && npm run build
cd server && dotnet format --verify-no-changes && dotnet build && dotnet test
```

The server's integration tests run against a real SQL Server: each run creates a
throwaway database through the migrations and drops it afterwards. Locally that is
`.\SQLEXPRESS`; set `RESELLTRACKER_TEST_SQL` to a connection string without a
database name to use another server.

CI runs the same on every push and pull request, with SQL Server in a service
container, and also fails if the EF model has changes no migration covers.
