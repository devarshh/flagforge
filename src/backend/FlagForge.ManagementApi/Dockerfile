# syntax=docker/dockerfile:1
# The management API: the dashboard backend.
# Build from the repository root: docker build -f src/backend/FlagForge.ManagementApi/Dockerfile .

# The SDK stage runs on the build machine's architecture and targets the image's (-a $TARGETARCH), which is fast
# on any host and publishes only the Linux native libraries the image needs.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# Restore in its own layer, so it reruns only when a project file or the shared settings change. .editorconfig sets
# analyzer severities, which matter because warnings are errors.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/backend/FlagForge.Evaluation/FlagForge.Evaluation.csproj src/backend/FlagForge.Evaluation/
COPY src/backend/FlagForge.Domain/FlagForge.Domain.csproj src/backend/FlagForge.Domain/
COPY src/backend/FlagForge.Application/FlagForge.Application.csproj src/backend/FlagForge.Application/
COPY src/backend/FlagForge.Infrastructure/FlagForge.Infrastructure.csproj src/backend/FlagForge.Infrastructure/
COPY src/backend/FlagForge.ServiceDefaults/FlagForge.ServiceDefaults.csproj src/backend/FlagForge.ServiceDefaults/
COPY src/backend/FlagForge.ManagementApi/FlagForge.ManagementApi.csproj src/backend/FlagForge.ManagementApi/
COPY src/backend/FlagForge.EvaluationApi/FlagForge.EvaluationApi.csproj src/backend/FlagForge.EvaluationApi/
COPY src/backend/FlagForge.Worker/FlagForge.Worker.csproj src/backend/FlagForge.Worker/
COPY src/backend/FlagForge.Migrator/FlagForge.Migrator.csproj src/backend/FlagForge.Migrator/
RUN dotnet restore src/backend/FlagForge.ManagementApi/FlagForge.ManagementApi.csproj -a $TARGETARCH

COPY src/backend/ src/backend/
RUN dotnet publish src/backend/FlagForge.ManagementApi/FlagForge.ManagementApi.csproj -c Release -a $TARGETARCH -o /app --no-restore /p:UseAppHost=false

# Chiseled images are distroless and run as a non-root user. The -extra variant adds ICU and time zone data, which
# Microsoft.Data.SqlClient needs (it refuses to run in globalization-invariant mode).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
ARG APP_VERSION=dev
ARG APP_COMMIT=local
ENV APP_VERSION=$APP_VERSION \
    APP_COMMIT=$APP_COMMIT
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "FlagForge.ManagementApi.dll"]
