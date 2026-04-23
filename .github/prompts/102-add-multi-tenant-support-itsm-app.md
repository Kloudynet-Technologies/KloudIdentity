# Summary of Changes: Current Branch vs dev-2.0

This document summarizes the key changes introduced in the current branch compared to `dev-2.0`.

## Major Features & Enhancements

- **Multi-Tenant Support**: TenantId is now included in key domain models, repositories, and APIs. All major user operations (create, update, delete, get, replace) now require and propagate TenantId.
- **ITSM Integration**: Added support for ITSM as a new integration method, including:
  - New domain models: `ItsmSettings`, `ItsmIntegrationMethod`, `ServiceProvider`, `ServiceProviderUrl`.
  - New integration logic in `ITSMIntegration`.
  - App configuration and attribute schema updates to support ITSM.
- **Metaverse Integration**: Added `IMetaverseIntegrationClient` and implementation for disconnected user provisioning, retrieval, update, replace, and deletion.
- **Action/Attribute Schema Extensions**: New action types and schema fields to support ITSM and metaverse flows.

## Key Code Changes

- **Domain Layer**
  - `AppConfig`, `AppConfigSnapshot`, and related repositories now include `TenantId`.
  - New ITSM-related classes and enums.
  - `IntegrationMethods` enum extended with `ITSM`.
- **Infrastructure Layer**
  - Repository methods updated to require `TenantId`.
  - New exception: `MetaverseIntegrationException`.
  - New client: `MetaverseIntegrationClient`.
  - EF migrations to add `TenantId` to `AppConfigSnapshots`.
- **MapperCore**
  - All user operation classes (`CreateUserV4`, `UpdateUserV4`, `DeleteUserV4`, `ReplaceUserV4`, `GetUserV4`) now use `TenantContext` and fetch config by tenant.
  - ITSM integration logic added and registered in DI.
- **WebHostSample**
  - Token generation now requires and encodes `TenantId` in JWT.
  - `ExtractAppIdFilter` and controllers extract and propagate `TenantId`.
  - DI registration for `TenantContext`.
- **Tests**
  - All relevant tests updated to mock and verify `TenantId` propagation.

## Migration Notes

- **Database**: Run the new migration to add the `TenantId` column to `AppConfigSnapshots`.
- **API**: All endpoints and clients must now provide `TenantId` (via header or JWT claim).
- **Configuration**: Update `appsettings.json` to include ITSM integration mapping.

## Impact

- Enables true multi-tenant operation and ITSM integration.
- Breaking changes for repository and API signatures (require `TenantId`).
- All provisioning logic is now tenant-aware and ready for disconnected/ITSM scenarios.