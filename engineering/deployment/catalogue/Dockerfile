# syntax=docker/dockerfile:1

# Multi-stage build: ONE image serving the .NET API + the Vue SPA. The build
# context MUST be engineering/codebase/, which holds BOTH ocharo-catalogue.backend-services
# and ocharo-catalogue.frontend-services.
#
#   docker build -f engineering/deployment/catalogue/Dockerfile -t ocharocatalogue engineering/codebase
#
#   docker run -d --name ocharocatalogue -p 127.0.0.1:8200:8080 -v ocharocatalogue-data:/data ocharocatalogue
#
# On first run the app creates /data/ocharocatalogue.db automatically.

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
RUN pnpm build

# ---- Stage 2: publish the .NET API (no Node here; the SPA arrives from stage 1) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0.300 AS build
WORKDIR /src/ocharo-catalogue.backend-services
COPY ocharo-catalogue.backend-services/ ./
COPY ocharo-sso/ /src/ocharo-sso/
# the built SPA lands in wwwroot so `dotnet publish` bundles it into the output.
COPY --from=spa /spa/apps/web/dist ./OcharoCatalogue.Api/wwwroot
RUN dotnet publish OcharoCatalogue.Api/OcharoCatalogue.Api.csproj -c Release -o /app -p:RestoreLockedMode=true -p:UseAppHost=false

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install --yes --no-install-recommends libfontconfig1 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app ./

# SQLite store on a volume so data survives container replacement.
ENV ConnectionStrings__OcharoCatalogue="Data Source=/data/ocharo-catalogue.db" \
    Catalogue__DataRoot=/data
RUN mkdir -p /data && chown app:app /data
VOLUME ["/data"]

USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "OcharoCatalogue.Api.dll"]
