# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

KloudIdentity is a SCIM 2.0 provisioning service built on .NET 8. It connects Entra ID (Azure AD) to Line-of-Business applications using pluggable integration methods (REST, SQL, AS400, Linux, ITSM). MassTransit/RabbitMQ handles inter-service messaging; Hangfire handles background jobs; EF Core + SQL Server stores app configuration.

## Commands

```bash
# Build
dotnet build

# Run (default port 5000)
dotnet run --project Microsoft.SCIM.WebHostSample

# Run DB migrations only then exit
dotnet run --project Microsoft.SCIM.WebHostSample -- migrate

# Add a new EF migration
dotnet ef migrations add <Name> \
  --project KN.KloudIdentity.Mapper.Infrastructure \
  --startup-project Microsoft.SCIM.WebHostSample

# Apply migrations manually
dotnet ef database update \
  --project KN.KloudIdentity.Mapper.Infrastructure \
  --startup-project Microsoft.SCIM.WebHostSample

# Run all tests
dotnet test

# Run a single test method
dotnet test --filter "FullyQualifiedName~<ClassName>.<MethodName>"

# Docker
docker build -t scimconnector-api:latest -f ./dockerfile .
```

## Solution Structure

| Project | Role |
|---|---|
| `Microsoft.SystemForCrossDomainIdentityManagement` | SCIM RFC 7644 protocol library (schemas, filtering, patch) |
| `Microsoft.SCIM.WebHostSample` | ASP.NET Core host — controllers, middleware, DI wiring |
| `KN.KloudIdentity.Mapper` | Core business logic — mapping engine, integration methods, auth strategies |
| `KN.KloudIdentity.Mapper.Domain` | Shared domain models, enums, DTOs |
| `KN.KloudIdentity.Mapper.Infrastructure` | EF Core DbContext (`KNContext`), repositories, `MetaverseIntegrationClient` |
| `KN.KloudIdentity.MapperTests` | xUnit + Moq unit tests |

## Key Architecture

### Integration Method Pattern

Every LOB integration implements `IIntegrationBaseV2` (`KN.KloudIdentity.Mapper/MapperCore/`). The interface covers the full SCIM lifecycle: `MapAndPreparePayloadAsync`, `GetAuthenticationAsync`, `ProvisionAsync`, `GetAsync`, `ReplaceAsync`, `UpdateAsync`, `DeleteAsync`, `ValidatePayloadAsync`.

Current implementations in `MapperCore/IntegrationMethods/`:
- `RESTIntegrationV4` — multi-step REST with action-based provisioning
- `RESTIntegrationV2` / `RESTIntegration` — legacy REST
- `SQLIntegration` — SQL Server direct
- `AS400Integration` — IBM AS/400
- `LinuxIntegration` — Linux system accounts
- `ITSMIntegration` — ITSM via internal metaverse service (MassTransit, no direct auth)

`IntegrationBaseFactory` selects the implementation at runtime from `AppConfig.IntegrationMethod`. App-to-integration mapping is also configurable in `appsettings.json` under `IntegrationMappings:AppIdToIntegration`.

New integrations must be registered in `KN.KloudIdentity.Mapper/Utils/ServiceExtension.cs`.

### Payload Mapping

`JSONParserUtilV2<T>.Parse(schema, resource)` converts a SCIM `Core2EnterpriseUser` into a `JObject` payload using `AttributeSchema` definitions. `DestinationField` uses **colon-separated** URN paths (`urn:kn:ki:schema:ExtendedProperties:ProjectKey`) — colons create nested JObject keys, dots do not.

### Authentication Strategies

`IAuthStrategy` implementations live in `MapperCore/Auth/`. Selected by `IAuthContext` based on `AppConfig.AuthenticationMethod`. Strategies: BasicAuth, OAuth2, ApiKey, DotRez (Navitaire-specific).

### DI Registration

All mapper services are registered in `ServiceExtension.cs::ConfigureMapperServices()`. Infrastructure services (EF, repositories, `MetaverseIntegrationClient`) are in `KN.KloudIdentity.Mapper.Infrastructure/DI/DependencyInjection.cs::AddInfrastructure()`. Both are called from `Startup.ConfigureServices`.

### Messaging (MassTransit / RabbitMQ)

- `IMetaverseIntegrationClient` → sends to `queue:metaverse_in` via `IRequestClient<IMetaverseServiceRequestMsg>` with a 60-second timeout
- `IMgtPortalServiceRequestMsg` → sends to `queue:mgtportal_in`
- `InterserviceConsumer` listens on `scimservice_in`
- `AppConfigStartupSync` (hosted service) syncs app config from MgtPortal on startup
- `IKloudIdentityLogger` also sends over MassTransit to the log aggregator service

### EF Core

`KNContext` is in `KN.KloudIdentity.Mapper.Infrastructure/Persistence/SQLServer/`. Migrations assembly is `KN.KloudIdentity.Mapper.Infrastructure`. Supports two SQL auth modes configured via `Database:AuthMode`: `"Sql"` (connection string) or `"Entra"` (token-based Azure SQL).

### Logging

Every operation-level event uses both Serilog (`Log.Information/Warning/Error`) for infrastructure tracing **and** `IKloudIdentityLogger.CreateLogAsync(CreateLogEntity)` for business audit logs. The `CreateLogEntity` constructor takes: `appId, logType, severity, eventInfo, logMessage, correlationId, loggerName, timestamp, user, null, null`.

## Configuration

Configuration is layered: `appsettings.json` → `appsettings.{env}.json` → User Secrets → Azure App Configuration (`KI:*` label) → Azure Key Vault. All app settings live under the `KI:` prefix. Required connection strings: `DefaultConnection` (SQL Server), `AppConfig` (Azure App Config), `HangfireDBConnection` (optional).
