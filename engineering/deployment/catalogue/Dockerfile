# syntax=docker/dockerfile:1

# Multi-stage build: ONE image serving the .NET API + the Vue SPA. The build
# context MUST be engineering/codebase/, which holds BOTH ocharo-catalogue.backend-services
# and ocharo-catalogue.frontend-services.
#
#   docker build -f engineering/deployment/catalogue/Dockerfile -t ocharocatalogue engineering/codebase
#
# Wheelhouse runs it from deploy.yml with the target's settings: it signs in through Ocharo's Identity and keeps its
# state in schema `catalogue` of the product database (ConnectionStrings:Database), so a bare `docker run` without
# those settings refuses to boot. The `catalogue-data` volume keeps the blobs stored before WoW2 Files and the retired
# SQLite store until `import-sqlite` copies it (catalogue.md § PostgreSQL move).

# ---- Stage 1: build the Vue workspace ----
FROM node:24.11.0-alpine AS spa
WORKDIR /spa
RUN corepack enable && corepack prepare pnpm@10.33.2 --activate
COPY ocharo-catalogue.frontend-services/package.json ocharo-catalogue.frontend-services/pnpm-workspace.yaml ocharo-catalogue.frontend-services/pnpm-lock.yaml ./
COPY ocharo-catalogue.frontend-services/apps/web/package.json ./apps/web/
RUN pnpm install --frozen-lockfile
# Copy authored inputs explicitly; host node_modules and generated output stay out.
COPY ocharo-catalogue.frontend-services/apps/web/index.html ocharo-catalogue.frontend-services/apps/web/vite.config.ts ocharo-catalogue.frontend-services/apps/web/vitest.config.ts ocharo-catalogue.frontend-services/apps/web/tsconfig*.json ./apps/web/
COPY ocharo-catalogue.frontend-services/apps/web/public/ ./apps/web/public/
COPY ocharo-catalogue.frontend-services/apps/web/src/ ./apps/web/src/
COPY ocharo-catalogue.frontend-services/apps/web/scripts/ ./apps/web/scripts/
COPY ocharo-catalogue.frontend-services/apps/web/tests/ ./apps/web/tests/
# CI passes the release version and commit (versioning.md); a local build reports `dev`. Declared here, after the
# installs, so a new version keeps their layers cached.
ARG APP_VERSION
ARG VCS_REF
RUN pnpm build

# ---- Stage 2: publish the .NET API (no Node here; the SPA arrives from stage 1) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0.300 AS build
WORKDIR /src/ocharo-catalogue.backend-services
COPY ocharo-catalogue.backend-services/ ./
# the built SPA lands in wwwroot so `dotnet publish` bundles it into the output.
COPY --from=spa /spa/apps/web/dist ./OcharoCatalogue.Api/wwwroot
ARG APP_VERSION
ARG VCS_REF
RUN dotnet publish OcharoCatalogue.Api/OcharoCatalogue.Api.csproj -c Release -o /app -p:RestoreLockedMode=true -p:UseAppHost=false -p:BuildSpa=false

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# curl serves the deployment's /health probe.
RUN apt-get update && apt-get install --yes --no-install-recommends curl libfontconfig1 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app ./

# The data volume: blobs stored before WoW2 Files, and the retired SQLite store (/data/ocharo-catalogue.db) and file key
# ring (/data/keys) that `import-sqlite` reads once, then leaves retired beside them.
ENV Catalogue__DataRoot=/data
RUN mkdir -p /data && chown app:app /data
VOLUME ["/data"]

USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "OcharoCatalogue.Api.dll"]
