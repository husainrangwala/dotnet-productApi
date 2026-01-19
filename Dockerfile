# 1. Use the .NET 10 SDK for the build stage
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy and restore
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish ProductApi.csproj -c Release -o out

# 2. Use the .NET 10 ASP.NET Runtime for the final stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Install required dependencies
RUN apt-get update && \
    apt-get install -y ca-certificates && \
    rm -rf /var/lib/apt/lists/*

# Copy the published app
COPY --from=build /app/out .

# --- NEW RELIC AGENT INSTALLATION FROM NUGET PACKAGE ---
# The agent files are already in the published output from NuGet package
# Just need to ensure proper permissions and setup
RUN mkdir -p /app/newrelic && \
    echo "[DOCKER BUILD] Setting up New Relic agent from NuGet package..." && \
    find /app -name "libNewRelicProfiler.so" -exec cp {} /app/newrelic/ \; 2>/dev/null || true && \
    find /app -name "newrelic.config" -exec cp {} /app/newrelic/ \; 2>/dev/null || true && \
    echo "[DOCKER BUILD] Contents of /app/newrelic:" && \
    ls -la /app/newrelic && \
    if [ -f /app/newrelic/libNewRelicProfiler.so ]; then \
        echo "[DOCKER BUILD] ✓ VERIFIED: libNewRelicProfiler.so EXISTS"; \
    else \
        echo "[DOCKER BUILD] ⚠️  libNewRelicProfiler.so not found - checking for agent files..."; \
        find /app -name "*.so" -path "*newrelic*" 2>/dev/null || echo "No .so files found"; \
        find /app/package* -name "libNewRelic*.so" 2>/dev/null || true; \
    fi

# --- NEW RELIC ENVIRONMENT CONFIGURATION ---
# These MUST be set as ENV (not ARG) for runtime
ENV CORECLR_ENABLE_PROFILING=1
ENV CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A}
ENV CORECLR_PROFILER_PATH=/app/newrelic/libNewRelicProfiler.so
ENV CORECLR_NEWRELIC_HOME=/app/newrelic
ENV NEW_RELIC_HOME=/app/newrelic

# New Relic License and App Name (will be set from build args or docker-compose)
ARG NEW_RELIC_LICENSE_KEY
ENV NEW_RELIC_LICENSE_KEY=${NEW_RELIC_LICENSE_KEY}
ENV NEW_RELIC_APP_NAME="Product CRUD API"
ENV NEW_RELIC_LOG_ENABLED=true
ENV NEW_RELIC_LOG_LEVEL=info

# ASP.NET Core Configuration
ENV ASPNETCORE_URLS=http://+:8080

# Expose port
EXPOSE 8080

ENTRYPOINT ["dotnet", "ProductApi.dll"]