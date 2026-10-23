# syntax=docker/dockerfile:1

# Multi-stage build: ONE image serving the .NET API + the Vue SPA. The build
# context MUST be engineering/codebase/, which holds BOTH ocharo-brand.backend-services
# and ocharo-brand.frontend-services.
#
#   docker build -f engineering/deployment/brand/Dockerfile -t ocharobrand engineering/codebase
#
#   docker run -d --name ocharobrand -p 127.0.0.1:8200:8080 -v ocharobrand-data:/data ocharobrand
#
# On first run the app creates /data/ocharo-brand.db automatically.

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
RUN pnpm build

# ---- Stage 2: publish the .NET API (no Node here; the SPA arrives from stage 1) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0.300 AS build
WORKDIR /src/ocharo-brand.backend-services
COPY ocharo-brand.backend-services/ ./
COPY ocharo-sso/ /src/ocharo-sso/
# the built SPA lands in wwwroot so `dotnet publish` bundles it into the output.
COPY --from=spa /spa/apps/web/dist ./OcharoBrand.Api/wwwroot
RUN dotnet publish OcharoBrand.Api/OcharoBrand.Api.csproj -c Release -o /app -p:RestoreLockedMode=true -p:UseAppHost=false

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# curl serves the deployment's /health probe.
RUN apt-get update && apt-get install --yes --no-install-recommends curl libfontconfig1 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app ./

# SQLite store on a volume so data survives container replacement.
ENV ConnectionStrings__OcharoBrand="Data Source=/data/ocharo-brand.db" \
    Brand__DataRoot=/data
RUN mkdir -p /data && chown app:app /data
VOLUME ["/data"]

USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "OcharoBrand.Api.dll"]
