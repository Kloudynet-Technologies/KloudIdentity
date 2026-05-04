---
name: Development Task (Plan-First)
about: Technical implementation plan for AI-assisted development
title: "[Dev Task] [SOAP Integration] EagleSOAPIntegration - Eagle Investment Systems Custom SOAP Connector"
labels: "Dev-Task, Plan-Pending"
assignees: ""
---

## Plan: EagleSOAPIntegration - Eagle Investment Systems Custom SOAP Connector


## 🟥 PART 1: ARCHITECTURAL CONTEXT & INTENT

**Introduction:**
Eagle Investment Systems exposes a hybrid API for user management: **SOAP for all write operations** (create, update, delete) via a single WSDL endpoint, and **REST for read operations** (`/eagle/v2/users`). The existing generic `SOAPIntegration` class cannot serve Eagle because:

1. Eagle returns a `<taskAcknowledgement>` ACK — not user data — from every write call. The base class parses for `<Identifier>`, `<UserName>`, and `<DisplayName>` and throws when they are absent.
2. Eagle provides no user identifier in its ACK. The user identifier must be extracted from the **outbound** EML payload before the request is sent.
3. Eagle reads require an **HTTP GET to a REST endpoint**, not a SOAP POST.
4. Every Eagle SOAP request requires a unique GUID `<eag1:correlationId>` injected at runtime into the envelope — `SOAPParserUtil.BuildPayload` only substitutes attribute-mapped placeholders, not runtime-generated values.
5. Eagle's WSDL requires an HTTP `SOAPAction: "RunTaskRequestSync"` header. The base `SendSoapRequestAsync` never sets this header.

The solution is a **subclass** `EagleSOAPIntegration : SOAPIntegration` that overrides only the behaviours that differ, with a companion `ISoapAuthApplier` (`EagleSoapActionApplier`) to inject the `SOAPAction` header into the existing applier chain without modifying the chain's architecture.

**Endpoint & Inputs:**

| Operation | Protocol | URI Pattern |
|-----------|----------|-------------|
| Create user | SOAP 1.1 | `%host%/EagleMLWebService20` (WSDL) |
| Update user | SOAP 1.1 | `%host%/EagleMLWebService20` (WSDL) |
| Delete user | SOAP 1.1 | `%host%/EagleMLWebService20` (WSDL) |
| Read user | REST GET | `%host%/eagle/v2/users?userid={id}&outputFormat=json` |

* **SOAP Method (all writes):** `RunTaskRequestSync`
* **EML Action tag per operation:** `<eag1:action>ADD</eag1:action>` / `CHANGE` / `DELETE`
* **Auth:** HTTP Basic Auth (`Authorization: Basic <base64>`) — Eagle User ID + Password

**Architectural Boundaries:**

* **Target Service:** `KN.KloudIdentity.Mapper` (MapperCore)
* **Core Patterns:** Subclass override pattern on `SOAPIntegration`; `ISoapAuthApplier` chain for request mutation; `IIntegrationBaseV2` contract for action-step-aware V4 user operations
* **Integration Resolution:** `IntegrationBaseFactory` resolves `EagleSOAPIntegration` for Eagle apps via `AppSettings.IntegrationMappings.AppIdToIntegration["EagleInvestment"] = "EagleSOAPIntegration"`
* **Infrastructure:** No new infrastructure. Reuses `IHttpClientFactory`, `IAuthContext` (`BasicAuthStrategy`), `SOAPParserUtil<T>`, `AppConfig.SOAPTemplates`, `AppConfig.UserURIs`

---

## 🟨 PART 2: IMPLEMENTATION PHASES (MILESTONES)

*Each phase must reach its checkpoint before the next phase begins. Phases 1–4 are implementation; Phase 5 is verification.*

---

### Phase 1: Base Class Accessibility Fixes

**File:** `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/SOAPIntegration.cs`

**Logic:** Five members are `private` or `private static` and must be accessible to the subclass. Change only visibility modifiers — zero logic changes.

| Member | Current | Target | Reason |
|--------|---------|--------|--------|
| `SendSoapRequestAsync(Uri, ...)` | `private async Task<string>` | `protected virtual async Task<string>` | All write-path overrides in the subclass call this method directly |
| `ValidateActionStep` | `private static` | `protected static` | V2 override methods guard null/empty endpoint via this helper |
| `MapHttpVerbToSoapAction` | `private static` | `protected static` | `DeleteAsync` V2 override resolves `SOAPActions` enum from `HttpVerbs` |
| `ResolveSoapTemplate` | `private static` | `protected static` | `DeleteAsync` V2 override retrieves the template for the action |
| `NormalizeAuthenticationDetails` | `private static` | `protected static` | `GetAsync` REST override normalizes `AuthenticationDetails` before setting HTTP headers |

**Verified:** All five members confirmed `private` / `private static` in current branch state. This phase is required.

**Agent Instruction:** "In `SOAPIntegration.cs`, change exactly these five member visibility modifiers as shown in the table. Do not change any method signatures, logic, or other members."

**Checkpoint:** `dotnet build Microsoft.SCIM.sln` passes with zero errors. No existing tests broken.

---

### Phase 2: EagleSoapActionApplier

**New file:** `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/SOAPAuth/EagleSoapActionApplier.cs`

**Logic:** A sealed, zero-dependency implementation of `ISoapAuthApplier` that adds the `SOAPAction: "RunTaskRequestSync"` HTTP header to every request. It is prepended to the auth applier chain in the `EagleSOAPIntegration` constructor — no changes to the base applier chain or to `ServiceExtension.cs` for the applier registrations.

```
class EagleSoapActionApplier : ISoapAuthApplier  (sealed, internal)
    const SoapActionValue = "\"RunTaskRequestSync\""

    ApplyAsync(SoapAuthContext context, CancellationToken ct):
        context.Request.Headers.Remove("SOAPAction")
        context.Request.Headers.TryAddWithoutValidation("SOAPAction", SoapActionValue)
        return Task.CompletedTask
```

**Namespace:** `KN.KloudIdentity.Mapper.MapperCore`

**Agent Instruction:** "Create `EagleSoapActionApplier.cs` as a `sealed internal` class in the namespace above. Implement only `ISoapAuthApplier`. No constructor parameters. The only logic is adding the `SOAPAction` HTTP header to `context.Request`."

**Checkpoint:** File compiles. No other files changed.

---

### Phase 3: EagleSOAPIntegration

**New file:** `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/EagleSOAPIntegration.cs`

**Namespace:** `KN.KloudIdentity.Mapper.MapperCore`

**Class:** `public class EagleSOAPIntegration : SOAPIntegration`

**Constant:** `private const string EagleNamespace = "http://www.eagleinvsys.com/2011/EagleML-2-0";`

#### 3.1 — Constructor

Same six parameters as base. Prepends `EagleSoapActionApplier` to the applier list so the `SOAPAction` header is set on every outgoing request.

```
EagleSOAPIntegration(authContext, httpClientFactory, configuration, appSettings, logger, soapAuthAppliers):
    base(..., [new EagleSoapActionApplier(), ...(soapAuthAppliers ?? DefaultAppliers())])

private static IEnumerable<ISoapAuthApplier> DefaultAppliers():
    return [new SoapTransportAuthApplier(), new WsSecuritySoapAuthApplier(), new SoapTokenHeaderApplier()]
```

#### 3.2 — MapAndPreparePayloadAsync override

Injects a fresh `Guid.NewGuid()` for `{{CorrelationId}}` before delegating to `base.MapAndPreparePayloadAsync`. Uses `appConfig with { SOAPTemplates = [patchedTemplate] }` — the original `AppConfig` record is never mutated.

```
override MapAndPreparePayloadAsync(schema, resource, appConfig, ct):
    template = appConfig.SOAPTemplates?.FirstOrDefault()
               ?? throw InvalidOperationException("SOAP template required. AppId: " + appConfig.AppId)

    injected = template.Template.Replace("{{CorrelationId}}", Guid.NewGuid().ToString(), Ordinal)
    patchedConfig = appConfig with { SOAPTemplates = [new SOAPTemplate(injected, template.Action)] }

    return base.MapAndPreparePayloadAsync(schema, resource, patchedConfig, ct)
```

#### 3.3 — ProvisionAsync V1 override

Eagle ACK contains no user identifier. Extract `userId` from the outbound EML payload **before** sending, then verify the ACK is positive.

```
override ProvisionAsync(payload, appConfig, correlationId, ct):
    userUri = appConfig.UserURIs?.FirstOrDefault()?.Post
              ?? throw InvalidOperationException("Eagle WSDL endpoint not configured.")
    userId = ExtractUserIdFromPayload((string)payload)
    responseBody = await SendSoapRequestAsync(userUri, payload, appConfig, Outbound, correlationId, ct)
    CheckEagleAck(responseBody, appConfig.AppId)
    return new Core2EnterpriseUser { Identifier = userId }
```

#### 3.4 — ProvisionAsync V2 override (ActionStep-aware)

Same logic as V1; endpoint comes from `actionStep.EndPoint`.

```
override ProvisionAsync(payload, appId, appConfig, actionStep, correlationId, ct):
    ValidateActionStep(actionStep, "PROVISION")
    userId = ExtractUserIdFromPayload((string)payload)
    responseBody = await SendSoapRequestAsync(new Uri(actionStep.EndPoint), payload, appConfig, Outbound, correlationId, ct)
    CheckEagleAck(responseBody, appConfig.AppId)
    return new Core2EnterpriseUser { Identifier = userId }
```

#### 3.5 — ReplaceAsync V1 override

Base V1 does not call `ParseSoapUserResponse` — `isNegative=true` ACKs would be silently ignored. Override adds the ACK check.

```
override ReplaceAsync(payload, resource, appConfig, correlationId):
    userUri = appConfig.UserURIs?.FirstOrDefault()?.Put
              ?? throw InvalidOperationException("Eagle Replace endpoint not configured.")
    responseBody = await SendSoapRequestAsync(userUri, payload, appConfig, Outbound, correlationId)
    CheckEagleAck(responseBody, appConfig.AppId)
```

> **Note — ReplaceAsync V2:** No override needed. Base V2 calls the polymorphic `ParseSoapUserResponse` which, when overridden in §3.8, throws on `isNegative=true` and returns an empty `Identifier` on success. The base's `if (!string.IsNullOrEmpty(parsedUser.Identifier))` guard then skips mutating the resource identifier — correct Eagle behaviour.

#### 3.6 — UpdateAsync V1 and V2 overrides

Base V1 and V2 do not call `ParseSoapUserResponse`, so `isNegative=true` ACKs are silently lost. Both overrides add the ACK check after sending.

```
override UpdateAsync(payload, resource, appConfig, correlationId):
    userUri = appConfig.UserURIs?.FirstOrDefault()?.Patch ?? .Put
              ?? throw InvalidOperationException("Eagle Update endpoint not configured.")
    responseBody = await SendSoapRequestAsync(userUri, payload, appConfig, Outbound, correlationId)
    CheckEagleAck(responseBody, appConfig.AppId)

override UpdateAsync(payload, resource, appId, appConfig, actionStep, correlationId, ct):
    ValidateActionStep(actionStep, "UPDATE")
    responseBody = await SendSoapRequestAsync(new Uri(actionStep.EndPoint), payload, appConfig, Outbound, correlationId, ct)
    CheckEagleAck(responseBody, appConfig.AppId)
```

#### 3.7 — DeleteAsync V1 and V2 overrides

Base builds the DELETE payload internally from `template.Template` without injecting `{{CorrelationId}}`. Both overrides inject the GUID before building, then check the ACK.

```
override DeleteAsync(identifier, appConfig, correlationId):
    userUri = appConfig.UserURIs?.FirstOrDefault()?.Delete
              ?? throw InvalidOperationException
    template = appConfig.SOAPTemplates?.FirstOrDefault(t => t.Action == SOAPActions.Delete)
               ?? throw InvalidOperationException
    attributes = appConfig.UserAttributeSchemas.Where(DELETE).ToList()
    resource = new Core2EnterpriseUser { Identifier = identifier }
    injected = template.Template.Replace("{{CorrelationId}}", Guid.NewGuid().ToString(), Ordinal)
    payload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(injected, attributes, resource)
    responseBody = await SendSoapRequestAsync(userUri, payload, appConfig, Outbound, correlationId)
    CheckEagleAck(responseBody, appConfig.AppId)

override DeleteAsync(identifier, appId, appConfig, actionStep, correlationId, ct):
    ValidateActionStep(actionStep, "DELETE")
    template = ResolveSoapTemplate(appConfig, MapHttpVerbToSoapAction(actionStep.HttpVerb, "DELETE"))
    attributes = actionStep.UserAttributeSchemas?.ToList()
                 ?? throw InvalidOperationException
    resource = new Core2EnterpriseUser { Identifier = identifier }
    injected = template.Template.Replace("{{CorrelationId}}", Guid.NewGuid().ToString(), Ordinal)
    payload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(injected, attributes, resource)
    responseBody = await SendSoapRequestAsync(new Uri(actionStep.EndPoint), payload, appConfig, Outbound, correlationId, ct)
    CheckEagleAck(responseBody, appConfig.AppId)
```

#### 3.8 — GetAsync V1 override (REST)

Eagle reads are REST-only. Do not send a SOAP POST.

```
override GetAsync(identifier, appConfig, correlationId, ct):
    restBaseUrl = appConfig.UserURIs?.FirstOrDefault()?.Get?.ToString()
                  ?? throw InvalidOperationException("Eagle REST GET URI not configured.")
    return await FetchEagleUserViaRestAsync(restBaseUrl, identifier, appConfig, ct)
```

#### 3.9 — GetAsync V2 override (ActionStep-aware, REST)

```
override GetAsync(identifier, appConfig, actionStep, correlationId, ct):
    ValidateActionStep(actionStep, "GET")
    return await FetchEagleUserViaRestAsync(actionStep.EndPoint, identifier, appConfig, ct)
```

#### 3.10 — ParseSoapUserResponse override

Eagle returns `<taskAcknowledgement>` not a user record. Parse `<eag1:isNegative>` and `<eag1:correlationId>`.

```
override ParseSoapUserResponse(responseBody):
    xmlDoc = new XmlDocument { XmlResolver = null }
    xmlDoc.LoadXml(responseBody)
    nsmgr.AddNamespace("eag1", EagleNamespace)

    isNegativeNode = xmlDoc.SelectSingleNode("//eag1:isNegative", nsmgr)
    if isNegativeNode?.InnerText.Equals("true", OrdinalIgnoreCase):
        throw InvalidOperationException("Eagle operation failed (isNegative=true). Response: " + responseBody)

    correlationNode = xmlDoc.SelectSingleNode("//eag1:correlationId", nsmgr)
    return new Core2EnterpriseUser { Identifier = correlationNode?.InnerText ?? string.Empty }
```

#### 3.11 — ExtractIdentifierFromSoapResponse override

Eagle ACK never contains a user identifier. Return `string.Empty` after verifying the ACK is positive — do not throw on empty.

```
override ExtractIdentifierFromSoapResponse(responseBody, appConfig):
    CheckEagleAck(responseBody, appConfig.AppId)
    return string.Empty
```

#### 3.12 — Private Helpers

**`CheckEagleAck(string responseBody, string appId)`**
- Parses `<eag1:isNegative>` from ACK XML using the Eagle namespace
- Throws `InvalidOperationException` with the full response body if value is `"true"` (case-insensitive)
- Called by all write-path overrides after receiving an HTTP response

**`ExtractUserIdFromPayload(string xmlPayload)`**
- Parses outbound EML XML; selects `//*[local-name()='id']` (exact element name to confirm against Eagle spec during implementation)
- Throws `InvalidOperationException` if element is absent or inner text is empty
- Called by `ProvisionAsync` V1 + V2 before the HTTP call

**`FetchEagleUserViaRestAsync(string baseUrl, string identifier, AppConfig appConfig, CancellationToken ct)`**
- Builds URL: `baseUrl.TrimEnd('/') + "?userid=" + Uri.EscapeDataString(identifier)`
- Retrieves token via `GetAuthenticationAsync(appConfig, Outbound, ct)`
- Creates `HttpClient` via `_httpClientFactory.CreateClient()`
- Sets auth headers via `HttpClientExtensions.SetAuthenticationHeaders(client, appConfig.AuthenticationMethodOutbound, NormalizeAuthenticationDetails(appConfig.AuthenticationDetails), token)`
- Issues `client.GetAsync(url, ct)` — **not POST**
- Calls `response.EnsureSuccessStatusCode()`
- Reads response body as string; calls `ParseEagleRestUserResponse(json, identifier)`

**`ParseEagleRestUserResponse(string json, string identifier)`** *(private static)*
- Deserializes Eagle REST user JSON
- Maps to `Core2EnterpriseUser`: `Identifier`, `UserName`, `DisplayName`
- Falls back to the `identifier` argument if the JSON `userId` field is absent

#### 3.13 — Method Override Matrix

| Method | V1 Override | V2 Override | Reason |
|--------|:-----------:|:-----------:|--------|
| `MapAndPreparePayloadAsync` | Yes | — | `{{CorrelationId}}` injection |
| `ProvisionAsync` | Yes | Yes | Identifier from payload, not ACK |
| `ReplaceAsync` | Yes | **No** | V1 needs ACK check; V2 base calls polymorphic `ParseSoapUserResponse` correctly |
| `UpdateAsync` | Yes | Yes | ACK check (base skips it for both overloads) |
| `DeleteAsync` | Yes | Yes | `{{CorrelationId}}` injection + ACK check |
| `GetAsync` | Yes | Yes | REST, not SOAP |
| `ParseSoapUserResponse` | Yes | — | ACK format differs from generic SOAP |
| `ExtractIdentifierFromSoapResponse` | Yes | — | No identifier in Eagle ACK |

**Agent Instruction:** "Implement `EagleSOAPIntegration.cs` strictly as specified in §3.1–§3.12. Override only the methods in the matrix above. Do not add any public methods not listed here. All private helpers must be `private` (not `protected` or `public`)."

**Checkpoint:** `dotnet build Microsoft.SCIM.sln` passes. `EagleSOAPIntegration` instantiates successfully in a unit test with mocked dependencies.

---

### Phase 4: DI Registration & AppSettings Configuration

**Files:**
- `KN.KloudIdentity.Mapper/Utils/ServiceExtension.cs`
- `Microsoft.SCIM.WebHostSample/appsettings.json` (or the relevant environment config)

**Current state of `ServiceExtension.cs` (post-merge, `integration/soap-into-dev2.0`):**

The `IIntegrationBaseV2` registration block currently reads, in order:

```csharp
services.AddScoped<IIntegrationBaseV2, RESTIntegrationV4>();   // formerly RESTIntegrationV2
services.AddScoped<IIntegrationBaseV2, ITSMIntegration>();      // added in dev2.0 merge
...
services.AddScoped<IIntegrationBaseV2, SOAPIntegration>();      // ← insertion point immediately after this line
```

`ITSMIntegration` uses `IntegrationMethod = ITSM` (enum value 7) and is resolved independently from SOAP integrations. It does not conflict with Eagle resolution.

**Logic — ServiceExtension.cs:** Add one line **immediately after** the `SOAPIntegration` registration (not after `ITSMIntegration`):

```csharp
services.AddScoped<IIntegrationBaseV2, SOAPIntegration>();
services.AddScoped<IIntegrationBaseV2, EagleSOAPIntegration>();  // ← add this line
```

`IntegrationBaseFactory` builds its lookup dictionary from `IList<IIntegrationBaseV2>`, which is resolved by `GetServices<IIntegrationBaseV2>().ToList()`. Both `SOAPIntegration` and `EagleSOAPIntegration` will be in the list; the factory selects between them by `AppIdToIntegration` map.

**Logic — AppSettings:** Add an entry in `IntegrationMappings.AppIdToIntegration`. The key is the Eagle `AppId` as registered in the system; the value is the exact class name used by `IntegrationBaseFactory`'s dictionary (keyed by `GetType().Name`):

```json
"IntegrationMappings": {
  "AppIdToIntegration": {
    "EagleInvestment": "EagleSOAPIntegration"
  },
  "DefaultIntegration": {
    "SOAP": "SOAPIntegration"
  }
}
```

All other SOAP applications continue resolving to `SOAPIntegration` via `DefaultIntegration`.

**Agent Instruction:** "Add the single `AddScoped` line in `ServiceExtension.cs` immediately after the `SOAPIntegration` registration (not after `ITSMIntegration`). Add the `AppIdToIntegration` entry to `appsettings.json`. No other changes."

**Checkpoint:** `dotnet build Microsoft.SCIM.sln` passes. Existing `ConfigureMapperServices_RegistersSoapAuthAndSoapIntegrationServices` unit test still passes.

---

### Phase 5: EagleSOAPIntegrationTests

**New file:** `KN.KloudIdentity.MapperTests/SOAPIntegration/EagleInvestment/EagleSOAPIntegrationTests.cs`

**Namespace:** `KN.KloudIdentity.MapperTests.SOAPIntegration.EagleInvestment`

#### 5.1 — Test Infrastructure

**`CreateSut(TestHttpMessageHandler? handler, string token)`**
- Instantiates `EagleSOAPIntegration` (not `SOAPIntegration`) with mocked `IAuthContext`, `IHttpClientFactory`, `IKloudIdentityLogger`, and `IOptions<AppSettings>`
- `IHttpClientFactory.CreateClient(It.IsAny<string>())` returns `new HttpClient(handler)`
- Default handler returns `EagleAckXml(isNegative: false, correlationId: "default-corr")` with HTTP 200

**`CreateAppConfig(...)`** — Eagle-specific defaults:
- `AuthenticationMethodOutbound = AuthenticationMethods.Basic`
- `AuthenticationDetails = new { Username = "eagleuser", Password = "eaglepass" }`
- `UserURIs.Post/Put/Patch/Delete` → `https://eagle.test/EagleMLWebService20`
- `UserURIs.Get` → `https://eagle.test/eagle/v2/users`
- Default `SOAPTemplates` = three templates (Create, Update, Delete), each with `{{CorrelationId}}` and `{{Identifier}}` placeholders

**`EagleAckXml(bool isNegative, string correlationId)`** — static helper building the ACK response XML:
```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"
               xmlns:eag1="http://www.eagleinvsys.com/2011/EagleML-2-0">
  <soap:Body>
    <eag1:taskAcknowledgement>
      <eag1:correlationId>{correlationId}</eag1:correlationId>
      <eag1:isNegative>{isNegative.ToString().ToLower()}</eag1:isNegative>
    </eag1:taskAcknowledgement>
  </soap:Body>
</soap:Envelope>
```

**`EagleRestUserJson(string userId, string displayName)`** — static helper for REST GET responses:
```json
{ "userId": "...", "name": "...", "emailAddress": "..." }
```

**`TestHttpMessageHandler`** — copied verbatim from `SOAPIntegrationUnitTests.cs`. Captures `LastRequestBody`, `LastAuthorizationHeader`, `LastHeaders`, and `LastRequestUri`.

#### 5.2 — Test Cases (28 total)

**Category 1 — Payload Validation (4 tests)**

| # | Test Name | Assert |
|---|-----------|--------|
| T-01 | `MapAndPreparePayloadAsync_InjectsValidGuidAsCorrelationId` | Output XML contains a `<correlationId>` whose inner text parses as a valid `Guid` |
| T-02 | `MapAndPreparePayloadAsync_TwoConsecutiveCalls_ProduceDifferentCorrelationIds` | Two calls produce two distinct GUID values in the output XML |
| T-03 | `MapAndPreparePayloadAsync_WithNoTemplate_ThrowsInvalidOperationException` | `appConfig.SOAPTemplates = null` → `ThrowsAsync<InvalidOperationException>` |
| T-04 | `MapAndPreparePayloadAsync_WithUserFields_MapsBothCorrelationIdAndAttributes` | Output XML contains a valid GUID, the `UserName`, and the `Identifier` value from the resource |

**Category 2 — Error Handling (6 tests)**

| # | Test Name | Assert |
|---|-----------|--------|
| T-05 | `ParseSoapUserResponse_WhenIsNegativeTrue_ThrowsInvalidOperationException` | Call override directly with `isNegative=true` ACK → `Throws<InvalidOperationException>` |
| T-06 | `ProvisionAsync_WhenAckIsNegative_ThrowsInvalidOperationException` | HTTP 200 with `isNegative=true` ACK → `ThrowsAsync<InvalidOperationException>` |
| T-07 | `ProvisionAsync_WithHttpFailure_ThrowsHttpRequestException` | HTTP 400 response → `ThrowsAsync<HttpRequestException>` |
| T-08 | `ProvisionAsync_WithSoapFault_ThrowsHttpRequestException` | HTTP 200 with `<soap:Fault>` body → `ThrowsAsync<HttpRequestException>` |
| T-09 | `GetAsync_WhenRestEndpointNotConfigured_ThrowsInvalidOperationException` | `UserURIs[0].Get = null` → `ThrowsAsync<InvalidOperationException>` |
| T-10 | `GetAsync_WhenRestReturns404_ThrowsHttpRequestException` | Handler returns HTTP 404 → `ThrowsAsync<HttpRequestException>` |

**Category 3 — Success Confirmation (5 tests)**

| # | Test Name | Assert |
|---|-----------|--------|
| T-11 | `ProvisionAsync_WithValidAck_ReturnsIdentifierFromPayload` | Payload XML contains `<id>john.doe</id>`; ACK `isNegative=false`; `result.Identifier == "john.doe"` |
| T-12 | `ProvisionAsyncV2_WithValidAckAndActionStep_ReturnsIdentifierFromPayload` | V2 overload with `ActionStep`; same identifier-from-payload assertion |
| T-13 | `ParseSoapUserResponse_WhenIsNegativeFalse_DoesNotThrow` | ACK with `isNegative=false`, `correlationId="c-001"` → no exception; `user.Identifier == "c-001"` |
| T-14 | `DeleteAsyncV2_WithValidAck_CompletesWithoutException` | `isNegative=false` ACK; template + attributes on `ActionStep` → no exception |
| T-15 | `UpdateAsyncV2_WithValidAck_CompletesWithoutException` | `isNegative=false` ACK → no exception |

**Category 4 — Action Mapping (4 tests)**

| # | Test Name | Assert |
|---|-----------|--------|
| T-16 | `ProvisionAsync_SetsSOAPActionHeader_RunTaskRequestSync` | `handler.LastHeaders["SOAPAction"] == "\"RunTaskRequestSync\""` |
| T-17 | `ProvisionAsync_SendsToWsdlEndpoint_NotRestEndpoint` | Captured request URL equals `UserURIs.Post` (WSDL), not `UserURIs.Get` |
| T-18 | `DeleteAsync_EmlBodyContainsActionDelete_FromTemplate` | `handler.LastRequestBody` contains `<eag1:action>DELETE</eag1:action>` |
| T-19 | `UpdateAsync_EmlBodyContainsActionChange_FromTemplate` | `handler.LastRequestBody` contains `<eag1:action>CHANGE</eag1:action>` |

**Category 5 — Conditional Logic (4 tests)**

| # | Test Name | Assert |
|---|-----------|--------|
| T-20 | `ExtractIdentifierFromSoapResponse_WhenAckPositive_ReturnsEmptyString` | Returns `string.Empty`; no exception |
| T-21 | `ExtractIdentifierFromSoapResponse_WhenAckNegative_ThrowsInvalidOperationException` | `Throws<InvalidOperationException>` |
| T-22 | `ProvisionAsync_WhenPayloadHasNoUserIdElement_ThrowsInvalidOperationException` | Payload with no `userId` element → throws before any HTTP call is made |
| T-23 | `MapAndPreparePayloadAsync_OriginalTemplateNotMutated_AfterCall` | `appConfig.SOAPTemplates[0].Template` still contains literal `{{CorrelationId}}` after the call |

**Category 6 — REST Integration for GET (5 tests)**

| # | Test Name | Assert |
|---|-----------|--------|
| T-24 | `GetAsync_IssuesHttpGetMethod_NotPost` | Captured `request.Method == HttpMethod.Get` |
| T-25 | `GetAsync_BuildsQueryUrl_WithUrlEncodedUserId` | Captured URL is `https://eagle.test/eagle/v2/users?userid=john.doe` |
| T-26 | `GetAsync_WhenRestReturnsValidJson_ReturnsMappedUser` | Handler returns Eagle user JSON; `result.Identifier == "u1"`, `result.DisplayName == "Alice"` |
| T-27 | `GetAsyncV2_UsesActionStepEndpoint_IgnoresUserUrisGet` | `actionStep.EndPoint = "https://eagle-v2.test/users"` → captured URL uses `eagle-v2.test` |
| T-28 | `GetAsyncV2_WithValidRestResponse_ReturnsMappedUser` | V2 overload; REST response parsed; `result.Identifier` correct |

**Agent Instruction:** "Implement all 28 test cases in a single file `EagleSOAPIntegrationTests.cs`. Use the `CreateSut`, `CreateAppConfig`, `EagleAckXml`, `EagleRestUserJson`, and `TestHttpMessageHandler` helpers defined in §5.1. Follow the exact naming convention of `SOAPIntegrationUnitTests.cs` (method name pattern: `Subject_Condition_ExpectedBehavior`). Each test must follow the Arrange-Act-Assert pattern with no shared mutable state between tests."

**Checkpoint:** `dotnet test KN.KloudIdentity.MapperTests/KN.KloudIdentity.MapperTests.csproj` — all 28 new tests pass; no pre-existing tests regress.

---

## 🟦 PART 3: TECHNICAL CONSTRAINTS & GUARDRAILS

* **Coding Standards:** C# 12 file-scoped namespaces; `record` types for any new domain objects; `is` pattern matching over explicit null checks; no `#region` blocks; primary constructors where dependencies are only assigned (not used in init logic)
* **Security:**
  * `XmlDocument.XmlResolver = null` on every XML parse — prevents XXE injection
  * No PII (usernames, passwords, identifiers) in `Log.Error` / `Log.Information` messages beyond what is already present in `SOAPIntegration`
  * `Uri.EscapeDataString` on any identifier placed into a REST URL query string
  * `System.Security.SecurityElement.Escape` is already applied by `SOAPParserUtil.BuildPayload` — do not double-escape
* **Performance:** `async/await` throughout; no `.Result` or `.Wait()` calls; no `new HttpClient(...)` directly — always use `_httpClientFactory`
* **Prohibited:**
  * Do not modify `SOAPIntegration` logic beyond the five visibility changes in Phase 1
  * Do not change `IIntegrationBase` or `IIntegrationBaseV2` interface contracts
  * Do not add new entries to `SOAPActions` enum or `IntegrationMethods` enum
  * Do not use `dynamic` in the new Eagle files except where the method signature inherits it from the base (`dynamic payload`)
  * Do not create a new `IAuthStrategy` — Eagle uses the existing `BasicAuthStrategy`
  * Do not add XML comments to test files; use descriptive test method names instead

---

## 🟩 PART 4: VERIFICATION & DEFINITION OF DONE

**Expected Output:**

| Operation | Scenario | Expected |
|-----------|----------|----------|
| Create | Valid EML payload, positive ACK | `Core2EnterpriseUser` returned with `Identifier` = `userId` from EML body |
| Create | Positive ACK, no `userId` in payload | `InvalidOperationException` before HTTP call |
| Create | `isNegative=true` ACK | `InvalidOperationException` after HTTP call |
| Update / Replace | Valid payload, positive ACK | Completes without exception; resource identifier unchanged |
| Delete | Valid identifier, positive ACK | Completes without exception |
| Read | Valid `userid`, REST 200 JSON | `Core2EnterpriseUser` with correct `Identifier` and display fields |
| Read | REST 404 | `HttpRequestException` |
| Any write | Eagle server returns SOAP Fault | `HttpRequestException` |
| Any write | HTTP non-2xx | `HttpRequestException` |

**Unit Test Scenarios (Definition of Done — all must be green):**

* [ ] **T-01 to T-04:** Payload Validation — `{{CorrelationId}}` injected as unique GUID; template immutability confirmed
* [ ] **T-05 to T-10:** Error Handling — `isNegative=true`, HTTP failures, SOAP Fault, missing config
* [ ] **T-11 to T-15:** Success Confirmation — positive ACK path for all write operations; REST GET success
* [ ] **T-16 to T-19:** Action Mapping — `SOAPAction` header present; correct WSDL endpoint used; correct EML `<action>` tag in body
* [ ] **T-20 to T-23:** Conditional Logic — empty identifier on positive ACK; payload userId extraction guard; immutability
* [ ] **T-24 to T-28:** REST Integration for GET — HTTP GET method; URL encoding; JSON parsing; V1 vs V2 endpoint selection

**Build Gate:** `dotnet build Microsoft.SCIM.sln` — zero errors, zero new warnings

**Test Gate:** `dotnet test KN.KloudIdentity.MapperTests/KN.KloudIdentity.MapperTests.csproj` — all 28 new tests pass; all pre-existing tests pass

---

## ⬜ PART 5: IMPACT & DEPENDENCIES

**Impacted Components:**

| Component | Nature of Change | Risk |
|-----------|-----------------|------|
| `SOAPIntegration.cs` | 5 visibility-only modifier changes | Low — non-breaking, no logic change |
| `ServiceExtension.cs` | 1 new `AddScoped` line after `SOAPIntegration` | Low — additive only |
| `appsettings.json` | 1 new `AppIdToIntegration` entry | Low — affects Eagle `AppId` only |
| `IntegrationBaseFactory` | No change — existing per-app lookup handles resolution | None |
| `ITSMIntegration` | Not touched — registered before `SOAPIntegration`; different `IntegrationMethod` enum value | None |
| All non-Eagle SOAP apps | No change — `DefaultIntegration["SOAP"]` still resolves to `SOAPIntegration` | None |

**Dependent Tasks:**

* Eagle `AppConfig` must be provisioned in the database with correct `AppId` matching the `AppIdToIntegration` key, `IntegrationMethodOutbound = SOAP`, three `SOAPTemplates` (Create/Update/Delete) containing full SOAP+EML envelopes with `{{CorrelationId}}` placeholders, and `UserURIs.Get` pointing to the Eagle REST base URL.
* Eagle API credentials (Username / Password) must be stored in `AppConfig.AuthenticationDetails`.

**New Files Delivered:**

```
KN.KloudIdentity.Mapper/
└── MapperCore/
    └── IntegrationMethods/
        ├── SOAPAuth/
        │   └── EagleSoapActionApplier.cs   (Phase 2)
        └── EagleSOAPIntegration.cs         (Phase 3)

KN.KloudIdentity.MapperTests/
└── SOAPIntegration/
    └── EagleInvestment/
        └── EagleSOAPIntegrationTests.cs    (Phase 5)
```

**Anti-Drift Log:** *(to be completed during implementation)*
* If the Eagle `userId` XPath expression differs from `//*[local-name()='id']`, update `ExtractUserIdFromPayload` and record the actual element name here.
* If Eagle returns a synchronous user record in the ACK body rather than a bare `<taskAcknowledgement>`, the `ParseSoapUserResponse` override must be revised — record any such discovery here.
