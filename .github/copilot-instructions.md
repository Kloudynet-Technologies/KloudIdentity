# Copilot Instructions for KloudIdentity SCIM Connector

## Project Overview

- This codebase implements a SCIM (System for Cross-domain Identity Management) endpoint and supporting infrastructure for user/group provisioning, following the SCIM RFC.
- Main solution: `Microsoft.SCIM.sln`.
- Core library: `Microsoft.SystemForCrossDomainIdentityManagement/` (protocol, schemas, service logic).
- Sample host: `Microsoft.SCIM.WebHostSample/` (web API, startup, config).
- Business logic, mapping, and integrations: `KN.KloudIdentity.Mapper/`, `KN.KloudIdentity.Mapper.Domain/`, `KN.KloudIdentity.Mapper.Infrastructure/`.

## Key Architectural Patterns

- **Separation of Concerns:**
  - `Schemas/`, `Protocol/`, `Service/` in the core library define SCIM resource models, protocol handling, and business logic.
  - `Controllers/` in the sample host expose SCIM endpoints, delegating to services.
  - `Mapper/`, `Domain/`, `Infrastructure/` follow DDD-inspired layering for extensibility.
- **Authentication/Authorization:**
  - Multiple strategies supported (see `Auth/` in Mapper): Basic, Bearer, OAuth2, API Key, custom (e.g., DotRez).
  - Auth is pluggable via `IAuthStrategy` and `IAuthContext`.
- **Background Jobs:**
  - Job execution and management interfaces in `BackgroundJobs/`.
- **Configuration:**
  - Centralized in `Config/` (e.g., `AuthConfig.cs`, `ConfigReaderSQL.cs`).

## Developer Workflows

- **Build:**
  - Use Visual Studio or `dotnet build Microsoft.SCIM.sln` from the root.
- **Run Sample API:**
  - Set `Microsoft.SCIM.WebHostSample` as startup project or run `dotnet run --project Microsoft.SCIM.WebHostSample`.
- **Test:**
  - Tests in `KN.KloudIdentity.MapperTests/`. Run with `dotnet test KN.KloudIdentity.MapperTests/KN.KloudIdentity.MapperTests.csproj`.
- **API Testing:**
  - Use Postman collections: `SCIM Inbound.postman_collection.json`.
- **Docker:**
  - Dockerfiles in root and `Microsoft.SCIM.WebHostSample/` for containerization.

## Project-Specific Conventions

- **Extensibility:**
  - Add new auth methods by implementing `IAuthStrategy`.
  - Add new background jobs by implementing `IInboundJobExecutor` and registering in DI.
- **Error Handling:**
  - Centralized in `Exceptions/` and resource files in the core library.
- **Naming:**
  - Folders and files are grouped by domain (e.g., `Consumers/`, `Utils/`, `Masstransit/`).
- **Integration:**
  - External API calls in `ExternalAPICalls/`.
  - MassTransit integration in `Masstransit/` folders.

## References

- See [README.md](../../README.md) for high-level guidance and links to the official SCIM spec and Azure AD integration docs.
- For detailed onboarding, see the [Wiki](https://github.com/AzureAD/SCIMReferenceCode/wiki) referenced in the README.

---

**Update this file if you introduce new architectural patterns, workflows, or conventions.**
