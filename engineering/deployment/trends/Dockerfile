# syntax=docker/dockerfile:1
# Build context: engineering/codebase.
FROM node:24.11.0-alpine AS spa
WORKDIR /spa
RUN corepack enable && corepack prepare pnpm@10.33.2 --activate
COPY ocharo-trends.frontend-services/package.json ocharo-trends.frontend-services/pnpm-workspace.yaml ocharo-trends.frontend-services/pnpm-lock.yaml ./
COPY ocharo-trends.frontend-services/apps/web/package.json ./apps/web/
RUN pnpm install --frozen-lockfile
COPY ocharo-trends.frontend-services/apps/web/index.html ocharo-trends.frontend-services/apps/web/vite.config.ts ocharo-trends.frontend-services/apps/web/tsconfig*.json ./apps/web/
COPY ocharo-trends.frontend-services/apps/web/src/ ./apps/web/src/
ARG APP_VERSION
RUN pnpm build

FROM mcr.microsoft.com/dotnet/sdk:10.0.300 AS build
WORKDIR /src
COPY ocharo-trends.backend-services/ ./
COPY --from=spa /spa/apps/web/dist ./OcharoTrends.Api/wwwroot
ARG APP_VERSION
RUN dotnet publish OcharoTrends.Api/OcharoTrends.Api.csproj -c Release -o /app -p:RestoreLockedMode=true -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install --yes --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app ./
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "OcharoTrends.Api.dll"]
