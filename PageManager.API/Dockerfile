# =========================
# RUNTIME (base)
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS base

# Non-root user oluştur
ARG APP_UID=10001
ARG APP_GID=10001
RUN groupadd --system --gid ${APP_GID} appgroup \
 && useradd  --system --uid ${APP_UID} --gid ${APP_GID} --create-home appuser

WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["PageManager.API/PageManager.API.csproj", "PageManager.API/"]
COPY ["PageManager.Application/PageManager.Application.csproj", "PageManager.Application/"]
COPY ["PageManager.Domain/PageManager.Domain.csproj", "PageManager.Domain/"]
COPY ["PageManager.Shared/PageManager.Shared.csproj", "PageManager.Shared/"]
COPY ["PageManager.Infrastructure/PageManager.Infrastructure.csproj", "PageManager.Infrastructure/"]

RUN dotnet restore "PageManager.API/PageManager.API.csproj"

COPY . .
WORKDIR /src/PageManager.API

RUN dotnet build "PageManager.API.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "PageManager.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish \
    --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN chown -R appuser:appgroup /app
USER appuser

ENTRYPOINT ["dotnet", "PageManager.API.dll"]
