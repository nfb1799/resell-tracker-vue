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

CI runs the integration tests against a SQL Server 2025 service container.

## Checks

```bash
cd client && npm run lint && npm test && npm run build
cd server && dotnet format --verify-no-changes && dotnet build && dotnet test
```

CI runs the same on every push and pull request.
