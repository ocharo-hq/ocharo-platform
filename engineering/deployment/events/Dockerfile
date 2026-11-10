# syntax=docker/dockerfile:1

# Multi-stage build: ONE image serving the .NET API + the Vue SPA. The build
# context MUST be engineering/codebase/, which holds BOTH ocharo-events.backend-services
# and ocharo-events.frontend-services.
#
#   docker build -f engineering/deployment/events/Dockerfile -t ocharoevents engineering/codebase
#
# Wheelhouse runs it from deploy.yml with the target's settings: it keeps its key ring in schema `ocharo_events` of the
# product database, signs in through Ocharo's Identity and reads WoW2 Events, so a bare `docker run` without those
# settings refuses to boot. It has no volume.

# ---- Stage 1: build the Vue workspace ----
FROM node:24.11.0-alpine AS spa
WORKDIR /spa
RUN corepack enable && corepack prepare pnpm@10.33.2 --activate
COPY ocharo-events.frontend-services/package.json ocharo-events.frontend-services/pnpm-workspace.yaml ocharo-events.frontend-services/pnpm-lock.yaml ./
COPY ocharo-events.frontend-services/apps/web/package.json ./apps/web/
RUN pnpm install --frozen-lockfile
# Copy authored inputs explicitly; host node_modules and generated output stay out.
COPY ocharo-events.frontend-services/apps/web/index.html ocharo-events.frontend-services/apps/web/vite.config.ts ocharo-events.frontend-services/apps/web/vitest.config.ts ocharo-events.frontend-services/apps/web/tsconfig*.json ./apps/web/
COPY ocharo-events.frontend-services/apps/web/dev/ ./apps/web/dev/
COPY ocharo-events.frontend-services/apps/web/public/ ./apps/web/public/
COPY ocharo-events.frontend-services/apps/web/src/ ./apps/web/src/
COPY ocharo-events.frontend-services/apps/web/scripts/ ./apps/web/scripts/
COPY ocharo-events.frontend-services/apps/web/tests/ ./apps/web/tests/
# CI passes the release version and commit (versioning.md); a local build reports `dev`. Declared here, after the
# installs, so a new version keeps their layers cached.
ARG APP_VERSION
ARG VCS_REF
RUN pnpm build

# ---- Stage 2: publish the .NET API (no Node here; the SPA arrives from stage 1) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0.300 AS build
WORKDIR /src/ocharo-events.backend-services
COPY ocharo-events.backend-services/ ./
# the built SPA lands in wwwroot so `dotnet publish` bundles it into the output.
COPY --from=spa /spa/apps/web/dist ./OcharoEvents.Api/wwwroot
ARG APP_VERSION
ARG VCS_REF
RUN dotnet publish OcharoEvents.Api/OcharoEvents.Api.csproj -c Release -o /app -p:RestoreLockedMode=true -p:UseAppHost=false -p:BuildSpa=false

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# curl serves the deployment's /health probe.
RUN apt-get update && apt-get install --yes --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app ./

# Sessions and the Data Protection key ring live in PostgreSQL; the events themselves in WoW2 Events. Nothing is written
# to the container's disk.
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "OcharoEvents.Api.dll"]
