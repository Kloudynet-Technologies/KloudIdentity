# Plan 118 — Tungsten AP Essentials Integration

**Branch:** `app/tungstenapessentials`
**Date:** 2026-06-15
**Author:** Ariful Islam
**API Doc:** `thaiunion-au-user-provisioning-guide.html` (AU tenant, verified 2026-06-12)

---

## Overview

Integrate Tungsten AP Essentials (ReadSoft Online) into KloudIdentity as a new integration method.
Tungsten uses a cookieless token auth flow — credentials (username/password) are exchanged for a
short-lived `ApsToken`. All API calls carry `Authorization: ApsToken Value="<token>"`.

UI configuration: `AuthenticationMethodOutbound = Basic` — no new auth strategy needed.
Credentials are read from `AuthenticationDetails` as `BasicAuthentication`.

---

## Base URL & Required Headers

```
Base URL:  https://services-au.readsoftonline.com
Auth URL:  {BaseUrl}/authentication/rest/authenticate

Fixed headers on every call:
  Content-Type:    application/json
  Accept:          application/json        ← required, else API returns XML
  x-rs-key:        <API_KEY>               ← from Azure App Config / Key Vault
  x-rs-version:    2011-10-14
  x-rs-culture:    en-US
  x-rs-uiculture:  en-US
```

> `x-rs-key` and fixed headers are stored in `AppSettings.AppIntegrationConfigs[appId].HttpSettings.Headers`
> (value resolved from Azure Key Vault via App Config reference).
> Applied automatically by `SetCustomHeaders` in `CreateHttpClientAsync`.

---

## Auth Flow

```
POST {BaseUrl}/authentication/rest/authenticate
Body: { "UserName": "...", "Password": "...", "AuthenticationType": 4 }

Response: { "Status": 1, "Token": "<aps_token>" }

All API calls: Authorization: ApsToken Value="<aps_token>"
```

Token validity: **30 minutes**. Cache window: **20 minutes** (safe margin).

---

## API Endpoints (verified against AU tenant)

| Operation | Verb   | Path                                              | Identifier used     |
|-----------|--------|---------------------------------------------------|---------------------|
| Create    | POST   | `/users/rest`                                     | —                   |
| Get       | GET    | `/users/rest/single/{0}/{1}`                      | `{0}` = orgId, `{1}` = **UserName** |
| Update    | PUT    | `/users/rest`                                     | **Id** (in body)    |
| Delete    | DELETE | `/users/rest/{0}`                                 | `{0}` = **Id** (GUID) |

### Identifier strategy

- `ProvisionAsync` → POST → parse `UserName` from response → return as `core2User.Identifier`
- `GetAsync` → `identifier` = UserName → `string.Format(getUri, organizationId, identifier)`
- `ReplaceAsync` / `UpdateAsync` → call `GetAsync` to resolve Tungsten `Id` → PUT with `Id` in body
- `DeleteAsync` → call `GetAsync` to resolve Tungsten `Id` → `string.Format(deleteUri, id)`

> **Why UserName as SCIM identifier?** The only GET endpoint is keyed by UserName
> (`/single/{orgId}/{userName}`). No GET-by-Id endpoint exists. Update and Delete need the
> GUID `Id` — resolved via `GetAsync` before each write operation.

### Minimum Create payload (verified)

```json
{
  "OrganizationId": "292d2d9924dd4ea0b5399d2ec8b0207b",
  "UserName": "john.smith",
  "FullName": "John Smith",
  "EmailAddress": "john.smith@example.com",
  "IsActive": true,
  "UseIdentityProvider": false
}
```

> Do NOT include `MonetaryLimit: { Value: 0 }` — causes `400` ("must specify a manager").
> `IsActive` is always `false` on creation until user completes login setup — expected behavior.

---

## Implementation Steps

### Step 1 — Add `OrganizationId` to `AppIntegrationConfig`

**File:** `KN.KloudIdentity.Mapper.Domain/AppSettings.cs`

```csharp
public class AppIntegrationConfig
{
    public string AppId { get; set; } = string.Empty;
    public HttpSettings? HttpSettings { get; set; }
    public string ClientType { get; set; } = string.Empty;
    public string? OrganizationId { get; set; }   // ← add this
}
```

Read in integration code:
```csharp
var config = _appSettings.AppIntegrationConfigs.FirstOrDefault(x => x.AppId == appConfig.AppId)
    ?? throw new InvalidOperationException($"AppIntegrationConfig not found for appId: {appConfig.AppId}");

var organizationId = config.OrganizationId
    ?? throw new InvalidOperationException($"OrganizationId not configured for appId: {appConfig.AppId}");
```

---

### Step 2 — Create `TungstenAPEssentialsIntegration` class

**File:** `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/Tug/TungstenAPEssentialsIntegration.cs`

Extends `RESTIntegration`. Overrides:

#### `IntegrationMethod` property
```csharp
IntegrationMethod = IntegrationMethods.REST;  // Tungsten is a REST app — factory resolves by AppIdToIntegration
```

#### Token cache (static — survives DI scopes since class is Scoped)
```
static ConcurrentDictionary<string, (string Token, DateTime ExpiresAt)>
  keyed by appId
  hit:  ExpiresAt > UtcNow  →  return cached token
  miss: POST authenticate, store with UtcNow.AddMinutes(20), return token
  401:  evict entry, re-authenticate once, retry
```

#### `GetAuthenticationAsync` override
1. Deserialize `config.AuthenticationDetails` → `BasicAuthentication` (Username, Password)
2. Validate both fields present
3. Check token cache — return if still valid
4. Apply custom headers (x-rs-key etc.) from `AppIntegrationConfigs` to the auth HTTP client
5. POST `{ UserName, Password, AuthenticationType: 4 }` to `{BaseUrl}/authentication/rest/authenticate`
6. Parse `Token` from JSON response body
7. Cache with 20-min expiry, return token

#### `CreateHttpClientAsync` override (protected)
1. Get token via `GetAuthenticationAsync`
2. Set `Authorization: ApsToken Value="<token>"`
3. Apply custom headers from `AppSettings.AppIntegrationConfigs` via `SetCustomHeaders`

#### `ProvisionAsync` override
1. Get `OrganizationId` from `AppIntegrationConfigs[appId].OrganizationId`
2. Build `User2` payload — `OrganizationId` merged into mapped payload
3. POST to `UserURIs[0].Post`
4. Parse response → `Identifier = response["UserName"]`

#### `GetAsync` override
1. Get `OrganizationId` from `AppIntegrationConfigs[appId].OrganizationId`
2. URL: `string.Format(userUri.Get.ToString(), organizationId, identifier)`
   → `/users/rest/single/{orgId}/{userName}`
3. Parse response → return `Core2EnterpriseUser { Identifier = userName, ExternalId = response["Id"] }`
   (ExternalId holds the Tungsten GUID `Id` for use by Replace/Delete)

#### `ReplaceAsync` / `UpdateAsync` override
1. Get `OrganizationId` from `AppIntegrationConfigs`
2. Call `GetAsync(resource.Identifier)` → get `ExternalId` (Tungsten GUID `Id`)
3. Build `User2` body with both `OrganizationId` and `Id`
4. PUT to `UserURIs[0].Put`

#### `DeleteAsync` override
1. Call `GetAsync(identifier)` → get `ExternalId` (Tungsten GUID `Id`)
2. URL: `string.Format(userUri.Delete.ToString(), externalId)`
   → `/users/rest/{id}`
3. DELETE

---

### Step 4 — Register in DI

**File:** `KN.KloudIdentity.Mapper/Utils/ServiceExtension.cs`

```csharp
services.AddScoped<IIntegrationBase, TungstenAPEssentialsIntegration>();
```

---

### Step 5 — Wire up in configuration

**File:** Azure App Configuration (`KI:AppIntegrationConfigs` key, per-environment label)

```json
[
  {
    "AppId": "<tungsten-appId>",
    "OrganizationId": "292d2d9924dd4ea0b5399d2ec8b0207b",
    "HttpSettings": {
      "Headers": {
        "x-rs-key": "@Microsoft.KeyVault(SecretUri=https://<vault>.vault.azure.net/secrets/TungstenApiKey/)",
        "x-rs-version": "2011-10-14",
        "x-rs-culture": "en-US",
        "x-rs-uiculture": "en-US"
      }
    }
  }
]
```

**File:** `Microsoft.SCIM.WebHostSample/appsettings.json`

```json
"IntegrationMappings": {
  "AppIdToIntegration": {
    "<tungsten-appId>": "TungstenAPEssentialsIntegration"
  }
}
```

**Azure Key Vault:** Add secret `TungstenApiKey` = actual `x-rs-key` GUID value.

> `OrganizationId` is NOT a secret — stored as plain text in Azure App Config.
> `x-rs-key` IS a secret — stored in Key Vault, referenced via `@Microsoft.KeyVault(...)`.
> Both are resolved automatically at startup via the existing `ConfigureKeyVault` wiring in `Program.cs`.

---

## What is NOT needed

- No new `IAuthStrategy` — auth logic lives entirely in the integration class override
- No new `AuthenticationMethods` enum value — UI stays as `Basic (= 1)`
- No new `IntegrationMethods` enum value — Tungsten is REST; factory resolves class via `AppIdToIntegration`
- No EF migration — `IntegrationMethodOutbound = REST (1)` in DB, no schema change
- No new domain model — `OrganizationId` added directly to existing `AppIntegrationConfig`

---

## Verified Gotchas (from API doc)

| Gotcha | Detail |
|--------|--------|
| `AuthenticationType` must be int `4` | Sending string `"SetBodyToken"` returns `400` |
| `Accept: application/json` required | Without it, API returns XML — must be on auth call too |
| `MonetaryLimit: { Value: 0 }` causes `400` | Omit the field entirely |
| `IsActive: true` silently ignored on create | New users stay inactive until logon email sent |
| `x-rs-key` is region-specific | AU key returns `401 Invalid API key` against other regions |
| DELETE returns `200` with full entity | Not `204` — parse body for audit log |
| GET non-existent user returns `404` | Use as "safe to create" signal |

---

## Approach Rationale

- Override approach (not `IAuthStrategy`) — `ApsToken Value="..."` header format is non-standard
- `OrganizationId` in `AppIntegrationConfig` (Option A) — everything in one place in Azure App Config,
  no management portal UI change needed, no new domain model
- `x-rs-key` in Key Vault via App Config reference — follows existing secret management pattern
