# ─── Stage 1: Build ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files first — Docker layer cache skips restore when unchanged
COPY DentalClinic.sln global.json ./
COPY DentalClinic.Web/DentalClinic.Web.csproj DentalClinic.Web/

RUN dotnet restore DentalClinic.Web/DentalClinic.Web.csproj

# Copy source and publish
COPY DentalClinic.Web/ DentalClinic.Web/
RUN dotnet publish DentalClinic.Web/DentalClinic.Web.csproj \
      --configuration Release \
      --output /app/publish \
      --no-restore

# ─── Stage 2: Runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install curl for HEALTHCHECK (minimal, cache cleared in same layer)
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copy published output, owned by the non-root 'app' user (.NET 9 image default)
COPY --from=build --chown=app:app /app/publish .

# Persistent directories for SQLite DB and Serilog rolling logs
RUN mkdir -p /app/data /app/logs && chown -R app:app /app/data /app/logs

USER app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
  CMD curl --fail --silent http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "DentalClinic.Web.dll"]
