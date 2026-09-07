# Build context is the repository root, because the shared build props and nuget.config live there.
#   docker build -f api/Dockerfile -t requestdesk-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, on the project files alone, so the package layer is cached until a dependency changes.
COPY Directory.Build.props Directory.Packages.props nuget.config ./
COPY api/RequestDesk.slnx api/dotnet-tools.json api/
COPY api/src/RequestDesk.Domain/RequestDesk.Domain.csproj api/src/RequestDesk.Domain/
COPY api/src/RequestDesk.Application/RequestDesk.Application.csproj api/src/RequestDesk.Application/
COPY api/src/RequestDesk.Infrastructure/RequestDesk.Infrastructure.csproj api/src/RequestDesk.Infrastructure/
COPY api/src/RequestDesk.Api/RequestDesk.Api.csproj api/src/RequestDesk.Api/
RUN dotnet restore api/src/RequestDesk.Api/RequestDesk.Api.csproj

COPY api/src api/src
RUN dotnet publish api/src/RequestDesk.Api/RequestDesk.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Attachments are written here; the compose file mounts a volume over it.
RUN mkdir -p /app/var/attachments && chown -R $APP_UID /app/var
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=5 \
  CMD ["/bin/sh", "-c", "wget -q -O /dev/null http://localhost:8080/health/live || exit 1"]

ENTRYPOINT ["dotnet", "RequestDesk.Api.dll"]
