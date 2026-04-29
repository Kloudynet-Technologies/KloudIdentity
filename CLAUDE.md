# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Commands

```bash
# Build
dotnet build Microsoft.SCIM.sln

# Run (HTTPS: 55860, HTTP: 55861)
dotnet run --project Microsoft.SCIM.WebHostSample

# Run all tests
dotnet test KN.KloudIdentity.MapperTests/KN.KloudIdentity.MapperTests.csproj

# Run a single test class
dotnet test KN.KloudIdentity.MapperTests/ --filter "FullyQualifiedName~CreateUserV4Tests"

# Docker build & run
docker build -t scimconnector-api -f ./dockerfile .
docker run -p 80:80 scimconnector-api
```

---

## Architecture Overview

This is a **.NET 8 SCIM provisioning connector** that bridges Azure AD (Entra ID) provisioning requests to Line-of-Business (LOB) applications via multiple integration protocols.

### Project Layout

| Project | Role |
|---------|------|
| `Microsoft.SystemForCrossDomainIdentityManagement/` | Microsoft SCIM protocol library — schemas, protocol parsing, service interfaces |
| `Microsoft.SCIM.WebHostSample/` | ASP.NET Core host — controllers, middleware, `Startup.cs`, `Program.cs` |
| `KN.KloudIdentity.Mapper/` | All business logic — user/group operations, integrations, auth, background jobs |
| `KN.KloudIdentity.Mapper.Domain/` | Domain models — `AppConfig`, `AttributeSchema`, `Action`, `ActionStep`, enums |
| `KN.KloudIdentity.Mapper.Infrastructure/` | Data access — EF Core (SQL Server), repositories, Azure Storage, queries |
| `KN.KloudIdentity.MapperTests/` | xUnit tests with Moq |

### Request Flow

```
Azure AD  →  SCIM Controllers (WebHostSample)
              ↓
           NonSCIMUserProvider / NonSCIMGroupProvider   (IProvider)
              ↓
           CreateUserV4 / UpdateUserV4 / ...            (ICreateResourceV2 etc.)
              ↓
           IIntegrationBaseFactory.GetIntegration()     (resolves by IntegrationMethods enum)
              ↓
           RESTIntegration / SOAPIntegration / SQLIntegration / ...
              ↓
           Target LOB API
```

All outbound user operations go through a class in `KN.KloudIdentity.Mapper/MapperCore/User/`. These classes inherit from `ProvisioningBase` and are wired in DI in `ServiceExtension.cs`.

---

## Versioned User Operations (V2 → V4)

User operation classes are versioned. **V4 is the active version** registered in DI (`ServiceExtension.cs`). Earlier versions (V2, V3) still exist but are not the active registrations.

**V4 adds two capabilities over V2:**

1. **Multi-step action pipeline** — `AppConfig.Actions` contains ordered `ActionStep` records, each with its own endpoint, HTTP verb, and attribute schemas. V4 iterates steps in `StepOrder` sequence and calls the `IIntegrationBaseV2` action-step-aware overloads.

2. **User migration** — if `AppSettings.UserMigration.AppFeatureEnabledMap[appId] == true`, `CreateUserV4` looks up existing user data in Azure Table Storage and routes to `ReplaceAsync` instead of creating a new user.

When there are no action steps configured, V4 falls back to the same single-call path as V2 (`ExecuteGenericUserCreationLogicAsync`).

---

## Integration System

**`IntegrationMethods` enum:** `REST=1`, `SOAP=2`, `SDK=3`, `Linux=4`, `AS400=5`, `SQL=6`

**`IIntegrationBaseFactory`** resolves the correct integration implementation at runtime. Resolution order:
1. Check `AppSettings.KI.IntegrationMappings.AppIdToIntegration` for a per-app override (e.g., `"Navitaire": "RESTIntegrationV2"`)
2. Fall back to `AppSettings.KI.IntegrationMappings.DefaultIntegration` (e.g., `"SOAP": "SOAPIntegration"`)

**Adding a new integration:** implement `IIntegrationBase` (standard) or `IIntegrationBaseV2` (supports action-step overloads), register it with `AddScoped`, and add a mapping in `AppSettings`.

**`IIntegrationBase`** defines the single-call contract (used by REST, Linux, AS400, SQL).  
**`IIntegrationBaseV2 : IIntegrationBase`** adds action-step-aware overloads used by V4 user operations and required by `SOAPIntegration`.

---

## AppConfig — Central Configuration Record

`AppConfig` (in `KN.KloudIdentity.Mapper.Domain/Application/`) is the runtime config for each registered LOB application. It is stored as a snapshot in SQL Server and loaded via `IAppConfigSnapshotRepository`.

Key fields:
- `IntegrationMethodOutbound` — determines which integration class is used
- `UserAttributeSchemas` / `GroupAttributeSchemas` — attribute mapping definitions (`AttributeSchema`)
- `UserURIs` — per-verb endpoints (`Post`, `Put`, `Patch`, `Delete`, `Get`)
- `Actions` → `ActionSteps` — multi-step pipeline config (V4 path)
- `SOAPTemplates` — list of `SOAPTemplate(Template, Action)` records; one template per `SOAPActions` value
- `SOAPAuthenticationOptions` — structured SOAP auth config (Transport/NTLM, WS-Security, TokenPlacement)
- `AuthenticationMethodOutbound` / `AuthenticationDetails` — standard auth config
- `IsExternalAPIEnabled` / `ExternalEndpointInfo` — optional custom logic webhook called between payload mapping and provisioning

---

## SOAP Integration

`SOAPIntegration` implements `IIntegrationBaseV2`. It is the only integration that takes its method-to-endpoint mapping from `ActionStep.EndPoint` (V4 path) rather than `AppConfig.UserURIs`.

**Payload building:** `SOAPParserUtil<T>.BuildPayload(template, schema, resource)` replaces `{{PlaceholderName}}` tokens in the XML template string. The entire SOAP envelope — including any protocol-specific outer wrapper — must be in the template.

**Auth applier chain** (runs in order on every request):
1. `SoapTransportAuthApplier` — NTLM credentials on `HttpClientHandler`, or Bearer/custom HTTP headers
2. `WsSecuritySoapAuthApplier` — injects `<wsse:Security><wsse:UsernameToken>` into `<soap:Header>`
3. `SoapTokenHeaderApplier` — injects an arbitrary XML fragment into `<soap:Header>` with `{{token}}` substitution

**Template scoping:** `ProvisioningBase.GetMappingConfigForSoapAction()` narrows `AppConfig.SOAPTemplates` to one template matching the current `SOAPActions` value before passing it to `MapAndPreparePayloadAsync`. This ensures `SOAPIntegration` always receives exactly one template.

---

## Authentication

`IAuthContext` (implemented by `AuthContextV2`) retrieves tokens by delegating to the registered `IAuthStrategy` implementations. Strategy selection is done by matching `AppConfig.AuthenticationMethodOutbound`.

Registered strategies: `ApiKeyStrategy`, `BasicAuthStrategy`, `BearerAuthStratergy`, `OAuth2Strategy`, `DotRezAuthStrategy`.

To add a new auth method: implement `IAuthStrategy`, register with `AddScoped`, and handle the new `AuthenticationMethods` enum value.

---

## DI Registration

All mapper DI is in `KN.KloudIdentity.Mapper/Utils/ServiceExtension.cs` → `ConfigureMapperServices()`.  
Infrastructure DI is in `KN.KloudIdentity.Mapper.Infrastructure/DI/DependencyInjection.cs` → `AddInfrastructure()`.

**Currently active user operation registrations:**
```
ICreateResourceV2  → CreateUserV3   (note: V3, not V4 — CreateUserV3 extends CreateUserV4)
IGetResourceV2     → GetUserV4
IReplaceResourceV2 → ReplaceUserV4
IUpdateResourceV2  → UpdateUserV4
IDeleteResourceV2  → DeleteUserV4
```

---

## Background Jobs & Messaging

- **Hangfire** handles recurring inbound sync jobs. Entry point: `IInboundJobExecutor` → `InboundJobExecutorService`.
- **MassTransit + RabbitMQ** handles inbound provisioning messages. Consumers are in `KN.KloudIdentity.Mapper/Consumers/`. Queue names: `scimsvc-in` / `scimsvc-out`.
- Inbound (AzureAD → connector) and outbound (connector → LOB) flows are distinct. Inbound uses `ICreateResourceInbound` / `IInboundMapper`.

---

## Testing

- Framework: **xUnit** with **Moq**
- Test files mirror the source structure under `KN.KloudIdentity.MapperTests/MapperCore/User/`
- All external dependencies (repositories, factories, integrations) are mocked via `Mock<T>`
- Tests are named `*Tests.cs` and use `[Fact]` attributes
