---
name: Development Task (Plan-First)
about: Technical implementation plan for AI-assisted development
title: "[Dev Task] [ASNBKioskIntegration] REST Integration - ASNB Kiosk Custom Auth Flow"
labels: "Dev-Task, Plan-Pending"
assignees: ""
---

## 🟥 PART 1: ARCHITECTURAL CONTEXT & INTENT

**Introduction:**
ASNB Kiosk is a REST-based target system that does **not** accept standard HTTP Basic (`Authorization: Basic <Base64>`) authentication. Instead, it exposes a proprietary token endpoint (`/TokenAuth/Authenticate`) that accepts a JSON username/password body and returns a short-lived JWT (`result.accessToken`). All subsequent API calls must present this JWT as `Authorization: Bearer <token>`.

The existing `BasicAuthStrategy` produces `Base64(username:password)` — which is the wrong credential format for ASNB. A new derived class `ASNBKioskIntegration` is needed that overrides only the authentication acquisition step while inheriting all CRUD provisioning logic from `RESTIntegrationV4`.

Credentials (username, encrypted password) are configured in MgtPortal using the existing `Basic` auth method. The auth endpoint base URL is derived at runtime from the GET action step endpoint already stored in the app's `AppConfig.Actions`, so no extra configuration fields are required.

**Endpoint & Inputs:**
- **Auth Route:** Derived at runtime — `{scheme}://{host}/{app}/api/TokenAuth/Authenticate` (built from GET action step endpoint)
- **Auth Payload:**
  ```json
  { "userNameOrEmailAddress": "<username>", "password": "<decrypted-password>" }
  ```
- **Auth Response (success path):** `result.accessToken` (JWT, expiry 86400s)
- **Downstream API Header:** `Authorization: Bearer <accessToken>`

**Architectural Boundaries:**
- **Target Service:** ASNB Kiosk REST API (`https://kiosk-dev.myasnb.com.my/ASNBAPI4/api/...`)
- **Core Patterns:** Derived class override — inherits `RESTIntegrationV4`, overrides `GetAuthenticationAsync` only
- **Infrastructure:** `ISecretManager` (Azure Key Vault) for password retrieval; `EncryptionHelper.Decrypt` for AES decryption; `IHttpClientFactory` for the auth HTTP call

**MgtPortal App Configuration:**

| Field | Value |
|---|---|
| Auth Method | `Basic` |
| Username | `admin` (maps to `userNameOrEmailAddress`) |
| Password | actual password, stored encrypted via Key Vault |
| AuthHeaderName | `Bearer` |
| Actions → GET → Step 1 EndPoint | `https://kiosk-dev.myasnb.com.my/ASNBAPI4/api/services/app/KMSUser/GetAllKMSUser` |

---

## 🟨 PART 2: IMPLEMENTATION PHASES (MILESTONES)

### Phase 1: Create `ASNBKioskIntegration` class skeleton

**Logic:**
- Create folder `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/PNB/`
- Create file `ASNBKioskIntegration.cs` in that folder
- Class inherits `RESTIntegrationV4`
- Constructor takes all `RESTIntegrationV4` parameters **plus** `ISecretManager secretManager`
- Set `IntegrationMethod = IntegrationMethods.REST` in the constructor body (no new enum value needed — routing is done via AppId mapping, not by integration method enum)
- Declare `private readonly ISecretManager _secretManager` and `private readonly AppSettings _appSettings`
- Declare `private const string TokenAuthPath = "/TokenAuth/Authenticate"`

**Agent Instruction:** "Create only the class skeleton with constructor. Do not implement any method bodies yet."

**Checkpoint:** Solution builds with no errors. `ASNBKioskIntegration` appears as a valid `IIntegrationBaseV2` implementation.

---

### Phase 2: Implement `BuildAuthUrl` private helper

**Logic:**
- Add `private static string BuildAuthUrl(string getEndpoint)` inside the class
- Parse `getEndpoint` using `new Uri(getEndpoint)`
- Split `uri.AbsolutePath` on `'/'` with `StringSplitOptions.RemoveEmptyEntries`
- Find the first segment equal to `"api"` (case-insensitive) using `Array.FindIndex`
- If not found: throw `InvalidOperationException` — *"Cannot derive ASNB auth URL: '/api' segment not found in endpoint '{getEndpoint}'."*
- Build and return: `$"{uri.Scheme}://{uri.Host}/{basePath}{TokenAuthPath}"`

**Example:**
- Input: `https://kiosk-dev.myasnb.com.my/ASNBAPI4/api/services/app/KMSUser/GetAllKMSUser`
- Segments: `["ASNBAPI4", "api", "services", ...]` → `apiIndex = 1`
- `basePath = "ASNBAPI4/api"`
- Output: `https://kiosk-dev.myasnb.com.my/ASNBAPI4/api/TokenAuth/Authenticate`

**Agent Instruction:** "Implement only the `BuildAuthUrl` static helper. Do not touch `GetAuthenticationAsync` yet."

**Checkpoint:** Unit test for `BuildAuthUrl` passes with the example URL above.

---

### Phase 3: Override `GetAuthenticationAsync`

**Logic — must match base signature exactly:**
```csharp
public override async Task<dynamic> GetAuthenticationAsync(
    AppConfig config,
    SCIMDirections direction = SCIMDirections.Outbound,
    CancellationToken cancellationToken = default,
    params dynamic[] args)   // args[0] is HttpClient from base — not used here
```

**Sub-step 3a — Extract Basic auth step and credentials:**
- Find step: `config.AuthenticationFlow?.Steps.FirstOrDefault(s => s.AuthenticationMethod == AuthenticationMethods.Basic)`
- If null: throw `AuthenticationException` — *"No Basic authentication step found in flow for app {config.AppId}."*
- Deserialize `step.AuthenticationDetails` as `BasicAuthentication`
- If null: throw `AuthenticationException` — *"Failed to deserialize BasicAuthentication for app {config.AppId}."*
- `encryptedPassword = await _secretManager.GetSecretAsync(auth.KeyVaultReference!)`
- `password = EncryptionHelper.Decrypt(encryptedPassword, _appSettings.EncryptionKey, auth.EncryptedData!.IV)`

**Sub-step 3b — Derive ASNB auth URL:**
- Find GET User action: `config.Actions?.FirstOrDefault(a => a.ActionName == ActionNames.GET && a.ActionTarget == ActionTargets.USER)`
- Get endpoint: `getAction?.ActionSteps?.FirstOrDefault()?.EndPoint`
- If null/empty: throw `InvalidOperationException` — *"No GET User action step endpoint configured for app {config.AppId}. Cannot derive ASNB auth URL."*
- `authUrl = BuildAuthUrl(getEndpoint)`

**Sub-step 3c — POST credentials to ASNB auth endpoint:**
- `using var authClient = _httpClientFactory.CreateClient()`
- Serialize body: `{ userNameOrEmailAddress = auth.Username, password = password }` as JSON
- `var response = await authClient.PostAsync(authUrl, content, cancellationToken)`
- If non-2xx: throw `AuthenticationException` — *"ASNB Kiosk auth HTTP call failed for app {config.AppId}. StatusCode: {statusCode}, Body: {body}"*

**Sub-step 3d — Parse and validate the token:**
- Parse response as `JObject`
- Check `responseJson["success"]?.Value<bool>() != true` → throw `AuthenticationException` with the `error` field value
- Extract `responseJson["result"]?["accessToken"]?.ToString()`
- If null/empty: throw `AuthenticationException` — *"ASNB Kiosk auth response missing accessToken for app {config.AppId}."*
- Log: `Log.Information("ASNB Kiosk auth token acquired for app {AppId}", config.AppId)`
- Return `new Dictionary<int, string> { [basicStep.StepOrder] = accessToken }`

**Why this works end-to-end:** The base `CreateHttpClientAsync` receives this dictionary, finds the matching `AuthenticationFlowStep` (the Basic step), and calls `HttpClientExtensions.SetAuthenticationHeaders(client, AuthenticationMethods.Basic, authDetails, accessToken)`. Because `BasicAuthentication.AuthHeaderName = "Bearer"`, this sets `Authorization: Bearer <accessToken>` on the `HttpClient` — exactly what ASNB Kiosk expects.

**Agent Instruction:** "Implement `GetAuthenticationAsync` following all 4 sub-steps. Do not override any other methods — all CRUD operations are inherited from `RESTIntegrationV4`."

**Checkpoint:** Integration manually tested or debugged; auth token acquired; first provisioning call reaches the ASNB API with correct `Authorization: Bearer` header.

---

### Phase 4: Register in DI and configure routing

**4a — Register in DI**

In `KN.KloudIdentity.Mapper/Utils/ServiceExtension.cs`, add one line alongside the existing `IIntegrationBaseV2` registrations (around line 78):
```csharp
services.AddScoped<IIntegrationBaseV2, ASNBKioskIntegration>();
```

No other DI changes needed. `ISecretManager` is already registered as `AddScoped` in `KN.KloudIdentity.Mapper.Infrastructure/DI/DependencyInjection.cs`.

**4b — Add AppId routing in `appsettings.json`**

In `Microsoft.SCIM.WebHostSample/appsettings.json`, under `IntegrationMappings.AppIdToIntegration`, add:
```json
"<asnbKioskAppId>": "ASNBKioskIntegration"
```
Replace `<asnbKioskAppId>` with the actual `AppId` value from the MgtPortal database. The string `"ASNBKioskIntegration"` must match the class name exactly — `IntegrationBaseFactory` does a dictionary lookup by class name.

**Agent Instruction:** "Add exactly one `AddScoped` line in `ServiceExtension.cs` and one key/value pair in `appsettings.json`. No other files should be touched in this phase."

**Checkpoint:** App starts without DI errors. `IntegrationBaseFactory.GetIntegration` resolves `ASNBKioskIntegration` when the ASNB AppId is passed.

---

### Phase 5: Write unit tests

**File:** `KN.KloudIdentity.MapperTests/MapperCore/IntegrationMethods/PNB/ASNBKioskIntegrationTests.cs`

**Test cases:**

| # | Test Name | Setup | Assert |
|---|---|---|---|
| 1 | `GetAuthenticationAsync_ReturnsJwt_WhenCredentialsAreValid` | Mock `ISecretManager` returns encrypted pw; mock `IHttpClientFactory` returns 200 with valid ASNB JSON body | Returned dictionary `[stepOrder]` equals `accessToken` from mock response |
| 2 | `GetAuthenticationAsync_Throws_WhenSuccessIsFalse` | Mock returns `{ "success": false, "error": "Invalid credentials" }` | Throws `AuthenticationException` containing "Invalid credentials" |
| 3 | `GetAuthenticationAsync_Throws_WhenAccessTokenMissing` | Mock returns `{ "success": true, "result": {} }` | Throws `AuthenticationException` containing "missing accessToken" |
| 4 | `GetAuthenticationAsync_Throws_WhenNoGetActionConfigured` | `appConfig.Actions` is empty | Throws `InvalidOperationException` containing "No GET User action step" |
| 5 | `GetAuthenticationAsync_Throws_WhenNoBasicStepInFlow` | `AuthenticationFlow.Steps` contains only a Bearer step | Throws `AuthenticationException` containing "No Basic authentication step" |
| 6 | `BuildAuthUrl_CorrectlyDerivesAuthUrl` | Input: `https://kiosk-dev.myasnb.com.my/ASNBAPI4/api/services/app/KMSUser/GetAllKMSUser` | Returns `https://kiosk-dev.myasnb.com.my/ASNBAPI4/api/TokenAuth/Authenticate` |
| 7 | `BuildAuthUrl_Throws_WhenApiSegmentMissing` | Input: `https://kiosk-dev.myasnb.com.my/noapi/services/users` | Throws `InvalidOperationException` containing "'/api' segment not found" |

**Agent Instruction:** "Write xUnit tests with Moq. Mock `IHttpClientFactory` using a fake `HttpMessageHandler`. Do not start a real HTTP server."

**Checkpoint:** `dotnet test --filter "FullyQualifiedName~ASNBKioskIntegration"` passes with all 7 tests green.

---

## 🟦 PART 3: TECHNICAL CONSTRAINTS & GUARDRAILS

- **Do NOT** add a new `IntegrationMethods` enum value. Routing is handled via `AppIdToIntegration` in `appsettings.json`. Set `IntegrationMethod = IntegrationMethods.REST`.
- **Do NOT** override `ProvisionAsync`, `GetAsync`, `UpdateAsync`, `ReplaceAsync`, or `DeleteAsync`. All CRUD logic is fully inherited from `RESTIntegrationV4`.
- **Do NOT** call `base.GetAuthenticationAsync()` inside the override. The base calls `_authContext.GetTokenListAsync`, which would invoke `BasicAuthStrategy` and return `Base64(user:pass)` — the wrong token format for ASNB.
- **Do NOT** call `new HttpClient()` directly. Use `_httpClientFactory.CreateClient()`.
- **Do NOT** log the raw password or the access token at any log level.
- **Do NOT** store the access token as a field or property. ASNB tokens expire in 86400s but the integration is scoped — re-authenticate per provisioning operation.
- **Security:** Raw password must be decrypted in memory only and not stored. Use `EncryptionHelper.Decrypt` in-method scope only.
- **Coding Standards:** C# file-scoped namespaces; no `async void`; `using var` for `HttpContent` and disposable `HttpClient`.
- **Prohibited:** No hardcoded hostnames or URLs. The ASNB host must always be derived from `appConfig.Actions`.

---

## 🟩 PART 4: VERIFICATION & DEFINITION OF DONE

**Expected Output:**

`GetAuthenticationAsync` returns `Dictionary<int, string>` where:
- Key = `basicStep.StepOrder` (integer matching the auth flow step)
- Value = ASNB JWT access token string

This is consumed by base `CreateHttpClientAsync` → `SetAuthenticationHeaders` → sets `Authorization: Bearer <JWT>` on the `HttpClient` used for all subsequent CRUD operations.

**Unit Test Scenarios:**
- [ ] Happy Path: Valid credentials → returns dictionary with JWT
- [ ] ASNB returns `success: false` → `AuthenticationException` thrown
- [ ] ASNB returns `success: true` but no `accessToken` → `AuthenticationException` thrown
- [ ] Non-2xx HTTP response from ASNB auth endpoint → `AuthenticationException` thrown
- [ ] `appConfig.Actions` has no GET User step → `InvalidOperationException` thrown
- [ ] `AuthenticationFlow` has no Basic step → `AuthenticationException` thrown
- [ ] GET endpoint has no `/api` segment → `InvalidOperationException` thrown

---

## ⬜ PART 5: IMPACT & DEPENDENCIES

**Impacted Components:**
- `KN.KloudIdentity.Mapper` — new file added, one `AddScoped` line added in `ServiceExtension.cs`
- `Microsoft.SCIM.WebHostSample` — one key/value added in `appsettings.json`
- `KN.KloudIdentity.MapperTests` — new test file added

**No changes to:**
- `IntegrationMethods` enum
- `AuthenticationMethods` enum
- `BasicAuthentication` domain model
- `HttpClientExtensions`
- `AuthContextV2` / `BasicAuthStrategy`
- Any Group provisioning files

**Dependent Tasks:**
- MgtPortal app record for ASNB Kiosk must exist with the correct `AppId` before the `appsettings.json` mapping can be confirmed
- Key Vault secret for the ASNB admin password must be created and the `KeyVaultReference` stored in the app's `AuthenticationDetails`

**Anti-Drift Log:**
- The original idea was to reuse `BasicAuthStrategy.GetTokenAsync` — this was ruled out because it returns `Base64(user:pass)`, not the ASNB JWT. The override must bypass `_authContext` entirely and perform the ASNB-specific POST flow directly.
- A new `IntegrationMethods.ASNBKiosk` enum value was considered but ruled out — not needed because routing is AppId-based, and all underlying operations are still REST.
