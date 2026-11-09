# syntax=docker/dockerfile:1

# Multi-stage build: ONE image serving the .NET API + the Vue SPA. The build
# context MUST be engineering/codebase/, which holds BOTH ocharo-brand.backend-services
# and ocharo-brand.frontend-services.
#
#   docker build -f engineering/deployment/brand/Dockerfile -t ocharobrand engineering/codebase
#
# Wheelhouse runs it from deploy.yml with the target's settings: it signs in through Ocharo's Identity, so a
# bare `docker run` without those settings refuses to boot. Its SQLite store lives on the `brand-data` volume.

# ---- Stage 1: build the Vue workspace ----
FROM node:24.11.0-alpine AS spa
WORKDIR /spa
RUN corepack enable && corepack prepare pnpm@10.33.2 --activate
COPY ocharo-brand.frontend-services/package.json ocharo-brand.frontend-services/pnpm-workspace.yaml ocharo-brand.frontend-services/pnpm-lock.yaml ./
COPY ocharo-brand.frontend-services/apps/web/package.json ./apps/web/
RUN pnpm install --frozen-lockfile
# Copy authored inputs explicitly; host node_modules and generated output stay out.
COPY ocharo-brand.frontend-services/apps/web/index.html ocharo-brand.frontend-services/apps/web/vite.config.ts ocharo-brand.frontend-services/apps/web/vitest.config.ts ocharo-brand.frontend-services/apps/web/tsconfig*.json ./apps/web/
COPY ocharo-brand.frontend-services/apps/web/dev/ ./apps/web/dev/
COPY ocharo-brand.frontend-services/apps/web/public/ ./apps/web/public/
COPY ocharo-brand.frontend-services/apps/web/src/ ./apps/web/src/
COPY ocharo-brand.frontend-services/apps/web/scripts/ ./apps/web/scripts/
COPY ocharo-brand.frontend-services/apps/web/tests/ ./apps/web/tests/
# CI passes the release version and commit (versioning.md); a local build reports `dev`. Declared here, after the
# installs, so a new version keeps their layers cached.
ARG APP_VERSION
ARG VCS_REF
RUN pnpm build

# ---- Stage 2: publish the .NET API (no Node here; the SPA arrives from stage 1) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0.300 AS build
WORKDIR /src/ocharo-brand.backend-services
COPY ocharo-brand.backend-services/ ./
# the built SPA lands in wwwroot so `dotnet publish` bundles it into the output.
COPY --from=spa /spa/apps/web/dist ./OcharoBrand.Api/wwwroot
ARG APP_VERSION
ARG VCS_REF
RUN dotnet publish OcharoBrand.Api/OcharoBrand.Api.csproj -c Release -o /app -p:RestoreLockedMode=true -p:UseAppHost=false -p:BuildSpa=false

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# curl serves the deployment's /health probe.
RUN apt-get update && apt-get install --yes --no-install-recommends curl libfontconfig1 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app ./

# SQLite store, blobs and the Data Protection key ring that encrypts sign-in sessions on a volume, so data and
# the keys survive container replacement.
ENV ConnectionStrings__OcharoBrand="Data Source=/data/ocharo-brand.db" \
    Brand__DataRoot=/data \
    DataProtection__KeysPath=/data/keys
RUN mkdir -p /data && chown app:app /data
VOLUME ["/data"]

USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "OcharoBrand.Api.dll"]
