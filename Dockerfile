# ── Stage 1: Build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and all project files first (layer-cache friendly)
COPY Azka.MaintenanceScheduling.sln ./
COPY Azka.Domain/Azka.Domain.csproj                             Azka.Domain/
COPY Azka.Persistence/Azka.Persistence.csproj                   Azka.Persistence/
COPY Azka.Presentation/Azka.Presentation.csproj                 Azka.Presentation/
COPY Azka.Services/Azka.Services.csproj                         Azka.Services/
COPY Azka.Services.Implementation/Azka.Services.Implementation.csproj  Azka.Services.Implementation/
COPY Azka.Shared/Azka.Shared.csproj                             Azka.Shared/
COPY Azka.Tests/Azka.Tests.csproj                               Azka.Tests/
COPY Azka.Web/Azka.Web.csproj                                   Azka.Web/

# Restore all dependencies
RUN dotnet restore

# Copy full source and publish
COPY . .
RUN dotnet publish Azka.Web/Azka.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Non-root user for security
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Azka.Web.dll"]
