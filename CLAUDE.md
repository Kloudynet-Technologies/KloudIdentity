# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build Microsoft.SCIM.sln
dotnet build -c Release --no-restore

# Run the sample API (http://localhost:5000)
dotnet run --project Microsoft.SCIM.WebHostSample

# Run all tests
dotnet test KN.KloudIdentity.MapperTests/KN.KloudIdentity.MapperTests.csproj --logger "console;verbosity=minimal"

# Run a single test class or method
dotnet test KN.KloudIdentity.MapperTests/KN.KloudIdentity.MapperTests.csproj --filter "FullyQualifiedName~ClassName"

# Docker build
docker build -t scimconnector-api:latest -f ./dockerfile .
```

## Architecture

This is a **SCIM 2.0 provisioning endpoint** (.NET 8) that bridges Azure AD/Entra ID with heterogeneous target systems (REST APIs, SQL databases, AS/400, Linux, ITSM). The solution is `Microsoft.SCIM.sln`.

### Project Layers

**`Microsoft.SystemForCrossDomainIdentityManagement/`** — Core SCIM protocol library (do not modify unless changing SCIM protocol behavior)
- `Schemas/`: User, Group, and complex attribute models per SCIM RFC
- `Protocol/`: Filtering, pagination, bulk operations, PATCH processing
- `Service/Controllers/`: REST endpoints for `/Users`, `/Groups`, `/Schemas`, `/ResourceTypes`, `/ServiceProviderConfig`

**`Microsoft.SCIM.WebHostSample/`** — ASP.NET Core host
- `Program.cs` / `Startup.cs`: Azure App Configuration, Key Vault, JWT auth, MassTransit, Hangfire, SCIM middleware registration
- `appsettings.json`: Token validation, RabbitMQ queue names, `IntegrationMappings` (app-ID-to-integration routing)
- `Provider/`: In-memory and non-SCIM provider implementations for development/testing

**`KN.KloudIdentity.Mapper/`** — Business logic and integration adapters
- `MapperCore/User/` and `MapperCore/Group/`: CRUD provisioning logic
- `MapperCore/IntegrationMethods/`: Adapter dispatch for REST, SQL, AS/400, Linux, ITSM integration types
- `MapperCore/Inbound/` / `MapperCore/Outbound/`: Request pipeline direction handling
- `Auth/`: Pluggable authentication strategies (see below)
- `BackgroundJobs/`: Hangfire job executors (`IInboundJobExecutor`)
- `Consumers/`: MassTransit message consumers
- `ExternalAPICalls/`: Outbound HTTP clients to target systems

**`KN.KloudIdentity.Mapper.Domain/`** — Domain models and application contracts
- `Application/AppConfig/`: Tenant application configuration (how each app maps attributes)
- `Mapping/`: `MappingTypes`, `AttributeSchema`, `MappingConditions`, `SCIMDirections`, `ObjectTypes`
- `Messaging/`: MassTransit message contracts and `ActionTypeEnum`
- `Itsm/`: ITSM-specific payload types

**`KN.KloudIdentity.Mapper.Infrastructure/`** — EF Core, Azure Table Storage, DI wiring
- `DI/`: Service registration extensions used by `Startup.cs`
- `ExternalAPICalls/`: Infrastructure implementations of integration client interfaces

**`KN.KloudIdentity.MapperTests/`** — xUnit tests using Moq and in-memory SQLite

### Key Patterns

**Authentication strategies** — Add new auth methods by implementing `IAuthStrategy`. Existing: `BasicAuthStrategy`, `BearerAuthStratergy`, `OAuth2Strategy`, `ApiKeyStrategy`, `DotRezAuthStrategy`. The strategy is resolved at runtime via `IAuthContext` / `AuthContextV2`.

**Integration method dispatch** — `MapperCore/IntegrationMethods/` routes provisioning operations to the correct adapter based on the app's configured integration type. Adding a new integration type means adding an adapter here and registering it in DI.

**Multi-tenant config** — Each tenant/app has an `AppConfig` stored in the database. Azure App Configuration (with Key Vault) provides environment-level secrets. The `IntegrationMappings` section in `appsettings.json` maps app IDs to integration types.

**Async messaging** — MassTransit + RabbitMQ handles asynchronous provisioning events. Queue names are configured under `RabbitMQ` in `appsettings.json`. Consumers live in `KN.KloudIdentity.Mapper/Consumers/`.

**Background jobs** — Hangfire manages recurring/scheduled provisioning jobs. Implement `IInboundJobExecutor` and register in DI to add a new job type.

### Data Flow

```
Azure AD → SCIM endpoint (/Users, /Groups)
  → Microsoft.SystemForCrossDomainIdentityManagement (protocol parsing)
  → KN.KloudIdentity.Mapper MapperCore (business logic, attribute mapping)
  → IntegrationMethods adapter (REST / SQL / AS400 / ITSM)
  → Target system
```

Inbound sync (target → SCIM) travels the reverse path through `MapperCore/Inbound/` and publishes events via MassTransit.
