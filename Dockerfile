# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Build stage
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the project files first so the restore layer is cached independently of
# source changes. Directory.Build.props must be present before restore because
# it injects the EF Core / Identity package references.
COPY ["Directory.Build.props", "./"]
COPY ["ApiGateway.csproj", "./"]
COPY ["ApiGateway.Contracts/ApiGateway.Contracts.csproj", "ApiGateway.Contracts/"]
RUN dotnet restore "ApiGateway.csproj"

# Copy the rest of the source and publish.
COPY . .
RUN dotnet publish "ApiGateway.csproj" \
        -c Release \
        -o /app/publish \
        /p:UseAppHost=false

# ---------------------------------------------------------------------------
# Runtime stage
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:4000 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 4000

COPY --from=build /app/publish .

# The container runs as the non-root user ($APP_UID), but COPY leaves /app owned by
# root, so SQLite cannot create identity.db there and fails with
# "SQLite Error 14: unable to open database file". Hand the data directory (and the
# app directory, which ocelot.json is written to) to the runtime user.
RUN mkdir -p /app/data && chown -R $APP_UID:$APP_UID /app /app/data

# Run as the non-root user shipped with the ASP.NET images.
USER $APP_UID

# Liveness probe: wget is available in the aspnet image, curl is not.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:4000/health || exit 1

ENTRYPOINT ["dotnet", "ApiGateway.dll"]