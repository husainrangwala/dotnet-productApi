# 1. Use the .NET 10 SDK for the build stage
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copy and restore
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o out

# 2. Use the .NET 10 ASP.NET Runtime for the final stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Install required dependencies
RUN apt-get update && \
    apt-get install -y ca-certificates && \
    rm -rf /var/lib/apt/lists/*

# Copy the published app
COPY --from=build /app/out .

# Create New Relic home directory
RUN mkdir -p /app/newrelic

# --- NEW RELIC ENVIRONMENT CONFIGURATION ---
ENV CORECLR_ENABLE_PROFILING=1
ENV CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A}
ENV CORECLR_NEWRELIC_HOME=/app
ENV CORECLR_PROFILER_PATH=/app/libNewRelicProfiler.so

# New Relic License and App Name
ARG NEW_RELIC_LICENSE_KEY
ENV NEW_RELIC_LICENSE_KEY=${NEW_RELIC_LICENSE_KEY}
ENV NEW_RELIC_APP_NAME="My Product CRUD API"
ENV NEW_RELIC_LOG_ENABLED=true
ENV NEW_RELIC_LOG_LEVEL=info

# ASP.NET Core Configuration
ENV ASPNETCORE_URLS=http://+:8080

# Expose port
EXPOSE 8080

ENTRYPOINT ["dotnet", "ProductApi.dll"]