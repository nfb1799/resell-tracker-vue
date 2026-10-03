# One image: the ASP.NET Core API serving the built Vue app from wwwroot, so the
# SPA and the API share an origin (see README). Built in CI; no local Docker needed.

# ── the client ───────────────────────────────────────────────────────────────
FROM node:24-alpine AS client
WORKDIR /src/client
COPY client/package.json client/package-lock.json ./
RUN npm ci
# The client imports the registry and field rules from ../shared.
COPY shared /src/shared
COPY client ./
# CI has already type-checked and tested; this only bundles.
RUN npm run build-only

# ── the server ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server
WORKDIR /src
COPY shared shared
COPY server server
RUN dotnet publish server/src/ResellTracker.Api -c Release -o /app
COPY --from=client /src/client/dist /app/wwwroot

# ── runtime ──────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=server /app .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
# The non-root user the .NET images ship with.
USER $APP_UID
ENTRYPOINT ["dotnet", "ResellTracker.Api.dll"]
