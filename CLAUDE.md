# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build / Run / Test

All projects target **net8.0**. Run from the repository root.

- Build everything: `dotnet build Microsoft.SCIM.sln`
- Run the API host: `dotnet run --project Microsoft.SCIM.WebHostSample`
- Run all tests: `dotnet test KN.KloudIdentity.MapperTests/KN.KloudIdentity.MapperTests.csproj`
- Run a single test: `dotnet test KN.KloudIdentity.MapperTests/KN.KloudIdentity.MapperTests.csproj --filter "FullyQualifiedName~SomeTestClass.SomeMethod"`
- Container build: `docker build -f dockerfile -t scim-connector .` (publishes `Microsoft.SCIM.WebHostSample` in **Debug** configuration; the `dockerfile` at the repo root is the production one)
- API smoke testing: import [PostmanCollection.json](PostmanCollection.json) (outbound) and [SCIM Inbound.postman_collection.json](SCIM%20Inbound.postman_collection.json) (inbound).

The test project uses **xUnit + Moq + Microsoft.Data.Sqlite.Core** (in-memory SQLite for repo tests; do not assume a real SQL Server is available).

## Solution layout (4 projects)

- [Microsoft.SystemForCrossDomainIdentityManagement/](Microsoft.SystemForCrossDomainIdentityManagement/) — vendored fork of the Microsoft SCIM reference library. `Schemas/` (resource models), `Protocol/` (filter/PATCH parsing, list responses), `Service/` (request handlers). Treat this as a third-party dependency; only edit if a SCIM-protocol bug requires it.
- [KN.KloudIdentity.Mapper.Domain/](KN.KloudIdentity.Mapper.Domain/) — pure domain models (no EF, no DI). Subfolders by aggregate: `Application/`, `Mapping/`, `Authentication/`, `Inbound/`, `As400/`, `SQL/`, `Setting/`, `License/`, `Masstransit/`, `Messaging/`, `ExternalEndpoint/`, `Shared/`, `Entities/`.
- [KN.KloudIdentity.Mapper/](KN.KloudIdentity.Mapper/) — application layer. Holds the integration strategies, auth strategies, MassTransit consumers, and Hangfire-driven inbound job executor.
- [KN.KloudIdentity.Mapper.Infrastructure/](KN.KloudIdentity.Mapper.Infrastructure/) — EF Core (`KNContext`), repositories, `SqlConnectionFactory`, EF migrations (`MigrationsAssembly = "KN.KloudIdentity.Mapper.Infrastructure"`), and the `AddInfrastructure` DI extension.
- [Microsoft.SCIM.WebHostSample/](Microsoft.SCIM.WebHostSample/) — the actual API host. Despite the "Sample" name this is **the** running service; do not treat it as throwaway sample code.

## Big-picture architecture

### Request entry: SCIM but not really

Inbound SCIM requests hit controllers in the SCIM library, which call an `IProvider`. The host wires `IProvider` to **`NonSCIMAppProvider`** ([Microsoft.SCIM.WebHostSample/Provider/NonSCIMAppProvider.cs](Microsoft.SCIM.WebHostSample/Provider/NonSCIMAppProvider.cs)), not the in-memory sample provider. `NonSCIMAppProvider` delegates to `NonSCIMUserProvider` / `NonSCIMGroupProvider`, which translate the SCIM operation into a downstream call against a non-SCIM target system (REST/SOAP/SQL/AS400/Linux). The legacy `Obsolete`-marked `CreateAsync(Resource, string)` overloads throw — always use the 3-arg overload that carries `appId`.

`ExtractAppIdFilter` pulls `appId` from the route/headers so providers know which tenant config to load.

### Integration strategy selection

Outbound calls go through `IIntegrationBaseV2` implementations in [KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/](KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/): `RESTIntegration`, `RESTIntegrationV2`, `RESTIntegrationV4`, `RESTManageEngineIntegration`, `SOAPIntegration`, `SQLIntegration`, `AS400Integration`, `LinuxIntegration`.

Resolution happens in [IntegrationBaseFactory.cs](KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/IntegrationBaseFactory.cs) and is **driven by configuration, not code**:

```jsonc
"IntegrationMappings": {
  "AppIdToIntegration": { "Navitaire": "RESTIntegrationV2", "pnb001": "RestIntegrationManageEngine" },
  "DefaultIntegration": { "REST": "RESTIntegrationV4", "SOAP": "SOAPIntegration", ... }
}
```

The factory first looks for an `appId` override, then falls back to the `DefaultIntegration` per integration method. **When you add a new integration class, register it in DI (multiple `IIntegrationBaseV2` are injected as `IList<>`) and add it to `appsettings.json` `IntegrationMappings`** — otherwise `GetIntegration` throws.

### Versioned User/Group operations

[MapperCore/User/](KN.KloudIdentity.Mapper/MapperCore/User/) and [MapperCore/Group/](KN.KloudIdentity.Mapper/MapperCore/Group/) contain `CreateUser`, `CreateUserV2`, `CreateUserV3`, `CreateUserV4`, etc. The interfaces (`ICreateResource`, `ICreateResourceV2`) split V1 from V2+. Newer versions are not strict supersets — picking the right version is tied to which integration class is selected for the app. When extending, prefer the V4 variants unless explicitly working on a legacy code path.

### Auth strategies (outbound, target system)

Pluggable via `IAuthStrategy` / `IAuthContext` in [KN.KloudIdentity.Mapper/Auth/](KN.KloudIdentity.Mapper/Auth/): `BasicAuthStrategy`, `BearerAuthStratergy` (sic), `OAuth2Strategy`, `ApiKeyStrategy`, `DotRezAuthStrategy`. There are two contexts (`AuthContextV1`, `AuthContextV2`) — V2 corresponds to the V2+ integration pipeline.

**SOAP auth** is a separate pipeline ([MapperCore/IntegrationMethods/SOAPAuth/](KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/SOAPAuth/)): a chain of `ISoapAuthApplier` implementations (`SoapTransportAuthApplier`, `WsSecuritySoapAuthApplier`, `SoapTokenHeaderApplier`) configured by `SOAPAuthenticationOptions` on `AppConfig`. Backward compat: if `SOAPAuthenticationOptions` is null the legacy `AuthenticationDetails` path runs unchanged.

### Inbound API auth (SCIM clients calling us)

JWT bearer, configured in [Startup.cs](Microsoft.SCIM.WebHostSample/Startup.cs). In **Development** all token validations are off; in non-dev `ValidateLifetime` is still `false` but issuer/audience/key are validated. `Token:*` settings come from the `KI:` section of Azure App Configuration.

### Configuration pipeline

`Program.cs` chains: `AddUserSecrets` → `AddAzureAppConfiguration` (selecting `KI:*` keys, label from `AppConfigLabel`, with Key Vault references resolved via `ClientSecretCredential` from `AzureCredentialHelper`). `KI:RefreshOption` triggers a full refresh every 5s.

Tenant-level config (`AppSettings`) is bound from the `KI` section. Per-app configuration is loaded from SQL through `AppConfigSnapshotRepository` and kept in sync via the `AppConfigSnapshotUpdatedConsumer` (RabbitMQ) and `AppConfigStartupSync` (hosted service that runs on boot).

### Messaging & background work

- **MassTransit + RabbitMQ** (`Startup.cs`): consumers `InterserviceConsumer` (queue `scimservice_in`) and `AppConfigSnapshotUpdatedConsumer`. Request clients exist for `IMgtPortalServiceRequestMsg` (`mgtportal_in`) and `IMetaverseServiceRequestMsg` (`metaverse_in`). Default exponential retry: 5 attempts, 1s→30s, ignoring `ArgumentException`.
- **Hangfire** for inbound provisioning jobs. SQL Server storage; if `Database:AuthMode == "Entra"`, connections are opened with an Azure AD access token from `AzureSqlTokenProvider`. Dashboard exposed at `/hangfire/jobs` (only when `HangfireDBConnection` is set). Job interfaces: `IInboundJobExecutor`, `IJobManagementService`.
- **Logging**: Serilog via `KN.KI.LogAggregator.SerilogInitializer`. `LoggingConfigs[0]` is required — startup throws `InvalidOperationException` if missing. There is also a custom `IKloudIdentityLogger` that publishes log events onto the bus.

### Persistence

EF Core 8 against SQL Server (`KNContext`). `SqlConnectionFactory.CreateAsync` resolves the connection — it supports both SQL auth and Entra-token auth driven by `Database:AuthMode`. Migrations live in `KN.KloudIdentity.Mapper.Infrastructure/Migrations` and that assembly is set as the migrations assembly in DI. `dotnet ef` commands must therefore be run with `--project KN.KloudIdentity.Mapper.Infrastructure` and `--startup-project Microsoft.SCIM.WebHostSample`.

## Conventions worth following

- **Don't add a new integration without updating `IntegrationMappings`** in `appsettings.json` — the factory uses `GetType().Name` as the lookup key.
- **Don't extend the legacy `Obsolete` provider overloads** — only the 3-arg `(Resource, appId, correlationId)` forms are real. The 2-arg ones throw.
- **Auth strategy class name has a typo** (`BearerAuthStratergy.cs`). Don't "fix" it casually — it is referenced by string in places.
- The "WebHostSample" project name is misleading: it is the actual production host. Treat it as a real project.
- The `Microsoft.SystemForCrossDomainIdentityManagement` library is a customised fork of Microsoft's reference SCIM code. Avoid drive-by edits.
- New auth methods → implement `IAuthStrategy` and register in DI. New background jobs → implement `IInboundJobExecutor` and register in DI.

## External docs

- README links to the upstream Microsoft [SCIM reference wiki](https://github.com/AzureAD/SCIMReferenceCode/wiki) for protocol-level questions.
- SOAP authentication options are documented in the README under **SOAP Authentication Options**.
