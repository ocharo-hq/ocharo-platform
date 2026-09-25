# syntax=docker/dockerfile:1

# Build context: engineering/codebase/.
FROM node:24.11.0-bookworm-slim AS spa
WORKDIR /src/brand-workspace.frontend-services
COPY brand-workspace.frontend-services/package.json brand-workspace.frontend-services/package-lock.json ./
RUN npm install --global npm@11.6.1 && npm ci
COPY brand-workspace.frontend-services/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0.300 AS build
WORKDIR /src/brand-workspace.backend-services
COPY brand-workspace.backend-services/ ./
COPY --from=spa /src/brand-workspace.backend-services/BrandWorkspace.Api/wwwroot/ ./BrandWorkspace.Api/wwwroot/
RUN dotnet publish BrandWorkspace.Api/BrandWorkspace.Api.csproj \
    --configuration Release --output /app/publish \
    -p:BuildSpa=false -p:UseAppHost=false -p:RestoreLockedMode=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish/ ./
ENV ASPNETCORE_URLS=http://+:8080
USER app
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent --show-error http://127.0.0.1:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "BrandWorkspace.Api.dll"]
