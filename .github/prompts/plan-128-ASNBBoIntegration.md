---
name: Development Task (Plan-First)
about: Technical implementation plan for AI-assisted development
title: "[Dev Task] [ASNBBoIntegration] REST Integration - CSV-to-Array Payload Transform for `reports`"
labels: "Dev-Task, Plan-Pending"
assignees: ""
---

## 🟥 PART 1: ARCHITECTURAL CONTEXT & INTENT

**Introduction:**
The ASNB Back Office (Bo) LOB application requires the user's report codes as a **primitive JSON string array** — e.g. `"reports": ["PAC01A", "PAC01B"]`. Entra ID attribute mappings cannot natively build a variable-length primitive array into a scalar SCIM extension slot (attempting `Split(...)` sends a JSON array into `ExtensionAttribute1`, which is modelled as a `string` and fails deserialization on ingestion).

The agreed design is therefore:
- **Entra side:** send the report codes as a single **comma-separated string** into `ExtensionAttribute1` (e.g. `"PAC01A,PAC01B"`). This deserializes cleanly into the existing scalar `ExtensionAttributeKIUserBase.ExtensionAttribute1`.
- **Connector side:** a new app-specific integration `ASNBBoIntegration` (deriving `RESTIntegrationV4`) overrides **only** `MapAndPreparePayloadAsync` to split that CSV string into a JSON array on the outbound payload before it is sent to the LOB app.

The built-in array support (`AttributeDataTypes.Array` in `JSONParserUtilV2.MakeJsonArray`) is **not** sufficient here: for a non-enumerable scalar source it wraps the *entire* string in a single element (`["PAC01A,PAC01B"]`) rather than splitting on commas. A code override is the correct mechanism.

**Endpoint & Inputs:**
- **Trigger:** Standard outbound user provisioning (POST/PUT) routed to this integration by AppId.
- **Inbound SCIM value:** `urn:ietf:params:scim:schemas:extension:ki:1.0:User:ExtensionAttribute1 = "PAC01A,PAC01B"` (comma-separated string from Entra `Constant`/`Expression` mapping).
- **Outbound target field:** `reports` — a top-level primitive string array in the LOB payload: `["PAC01A", "PAC01B"]`.

**Architectural Boundaries:**
- **Target Service:** ASNB Back Office REST API.
- **Core Patterns:** Derived-class override — inherits `RESTIntegrationV4`, overrides `MapAndPreparePayloadAsync` **only**. All auth + CRUD logic inherited unchanged.
- **Routing:** AppId-based dispatch via `IntegrationBaseFactory` + `appsettings.json` (`IntegrationMappings.AppIdToIntegration`). No new `IntegrationMethods` enum value.
- **Payload builder:** `JSONParserUtilV2<Resource>.Parse` (invoked by base `MapAndPreparePayloadAsync`).

**MgtPortal / App Configuration (outbound schema mapping):**

| Field | Value |
|---|---|
| Source attribute | `...:extension:ki:1.0:User:ExtensionAttribute1` |
| Destination field | `urn:kn:ki:schema:reports` (top-level `reports`) |
| Mapping type | `Direct` |
| Destination type | `String`  ← *keep as String; the override does the split* |

> The schema maps `ExtensionAttribute1 → reports` as a plain **String**, so the base builder emits `"reports": "PAC01A,PAC01B"`. The override then rewrites that node as an array. Do **not** configure `reports` as `Array` in the schema — that path does not split on commas.

---

## 🟨 PART 2: IMPLEMENTATION PHASES (MILESTONES)

### Phase 1: Create `ASNBBoIntegration` class skeleton

**Logic:**
- Create file `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/PNB/ASNBBoIntegration.cs` (same folder as `ASNBKioskIntegration`).
- Namespace: `KN.KloudIdentity.Mapper.MapperCore` (match `ASNBKioskIntegration`).
- Class `public class ASNBBoIntegration : RESTIntegrationV4`.
- Constructor mirrors the base `RESTIntegrationV4` constructor signature exactly (`IAuthContext`, `IHttpClientFactory`, `IConfiguration`, `IKloudIdentityLogger`, `IOptions<AppSettings>`) and passes them straight to `base(...)`. **No** `ISecretManager` needed — standard inherited auth is used.
- In the constructor body set `IntegrationMethod = IntegrationMethods.REST`.
- Declare a private const for the target field name: `private const string ReportsFieldName = "reports";` and a delimiter const `private const char CsvDelimiter = ',';`.

**Agent Instruction:** "Create only the class skeleton and constructor. Do not implement `MapAndPreparePayloadAsync` yet."

**Checkpoint:** Solution builds. `ASNBBoIntegration` is a valid `IIntegrationBaseV2`/`RESTIntegrationV4` subtype.

---

### Phase 2: Override `MapAndPreparePayloadAsync`

**Signature — must match base exactly** ([RESTIntegrationV4.cs:195](../../KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/RESTIntegrationV4.cs)):
```csharp
public override async Task<dynamic> MapAndPreparePayloadAsync(
    IList<AttributeSchema> schema,
    Core2EnterpriseUser resource,
    CancellationToken cancellationToken = default)
```

**Sub-step 2a — Build the base payload:**
- `var payload = await base.MapAndPreparePayloadAsync(schema, resource, cancellationToken);`
- Normalize to `JObject`: `JObject jPayload = payload as JObject ?? JObject.FromObject(payload);`

**Sub-step 2b — Locate the `reports` node:**
- Use `jPayload.SelectToken(ReportsFieldName)` (top-level). If the token is absent or `null` → log at `Debug`/`Information` and return `jPayload` unchanged (nothing to transform).

**Sub-step 2c — Convert CSV → JArray:**
- Only transform when the token is a `JValue` of string type (i.e. the base produced `"reports": "PAC01A,PAC01B"`). Guard: `token.Type == JTokenType.String`.
- Split:
  ```csharp
  var codes = token.Value<string>()!
      .Split(CsvDelimiter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
  ```
- Replace the node with a `JArray`: `token.Replace(new JArray(codes));` (or set `jPayload[ReportsFieldName] = new JArray(codes);` for a top-level field).
- Edge cases:
  - Empty/whitespace CSV → results in an empty `string[]` → set `new JArray()` (empty array). Confirm this is acceptable to the LOB; if the field should be omitted when empty, remove the property instead (decide with the API owner — default: emit `[]`).
  - Already a `JArray` (e.g. schema later changed to Array) → leave unchanged, do not double-process.

**Sub-step 2d — Return:**
- `return jPayload;`
- Add a single `Log.Information` line noting the transform occurred for `resource.Identifier` (count of codes). **Do not** log full PII payloads.

**Agent Instruction:** "Implement `MapAndPreparePayloadAsync` per sub-steps 2a–2d. Call `base.MapAndPreparePayloadAsync` first; only post-process the `reports` node. Do not override any other method."

**Checkpoint:** Given a resource whose mapped payload contains `"reports": "PAC01A,PAC01B"`, the returned `JObject` contains `"reports": ["PAC01A","PAC01B"]`.

---

### Phase 3: Register in DI and configure routing

**3a — DI registration** — in [ServiceExtension.cs:80](../../KN.KloudIdentity.Mapper/Utils/ServiceExtension.cs), alongside `ASNBKioskIntegration`:
```csharp
services.AddScoped<IIntegrationBaseV2, ASNBBoIntegration>();
```

**3b — AppId routing** — in [appsettings.json](../../Microsoft.SCIM.WebHostSample/appsettings.json), under `IntegrationMappings.AppIdToIntegration` (next to `"asnbkiosk": "ASNBKioskIntegration"`):
```json
"<asnbBoAppId>": "ASNBBoIntegration"
```
Replace `<asnbBoAppId>` with the actual AppId from MgtPortal. The value string must equal the class name exactly — `IntegrationBaseFactory` keys the dictionary on `GetType().Name` ([IntegrationBaseFactory.cs:17](../../KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/IntegrationBaseFactory.cs)).

**Agent Instruction:** "Add exactly one `AddScoped` line and one `AppIdToIntegration` entry. No other files touched in this phase."

**Checkpoint:** App starts without DI errors; `IntegrationBaseFactory.GetIntegration(REST, asnbBoAppId)` resolves `ASNBBoIntegration`.

---

### Phase 4: Unit tests

**File:** `KN.KloudIdentity.MapperTests/MapperCore/PNB/ASNBBoIntegrationTests.cs` (mirror `ASNBKioskIntegrationTests` construction).

| # | Test Name | Setup | Assert |
|---|---|---|---|
| 1 | `MapAndPreparePayload_SplitsCsv_IntoArray` | Schema maps `ExtensionAttribute1 → reports` (String); resource `ExtensionAttribute1 = "PAC01A,PAC01B"` | `payload["reports"]` is `JArray` == `["PAC01A","PAC01B"]` |
| 2 | `MapAndPreparePayload_SingleValue_ProducesSingleElementArray` | `ExtensionAttribute1 = "PAC01A"` | `payload["reports"]` == `["PAC01A"]` |
| 3 | `MapAndPreparePayload_TrimsWhitespace` | `ExtensionAttribute1 = "PAC01A, PAC01B ,PAC02C"` | == `["PAC01A","PAC01B","PAC02C"]` |
| 4 | `MapAndPreparePayload_Empty_ProducesEmptyArray` | `ExtensionAttribute1 = ""` | `payload["reports"]` == `[]` (or omitted, per agreed behavior) |
| 5 | `MapAndPreparePayload_NoReportsField_ReturnsPayloadUnchanged` | schema has no `reports` mapping | payload returned unchanged; no exception |
| 6 | `MapAndPreparePayload_AlreadyArray_NotDoubleProcessed` | `reports` already a `JArray` | value unchanged |
| 7 | `MapAndPreparePayload_LeavesOtherFieldsIntact` | payload has `userName`, `reports` | non-`reports` fields identical to base output |

**Agent Instruction:** "Write xUnit + Moq tests. Build the `AttributeSchema` list and `Core2EnterpriseUser` in-memory; assert on the returned `JObject`. No real HTTP."

**Checkpoint:** `dotnet test --filter "FullyQualifiedName~ASNBBoIntegration"` — all green.

---

## 🟦 PART 3: TECHNICAL CONSTRAINTS & GUARDRAILS

- **Override scope:** Override **only** `MapAndPreparePayloadAsync`. Do **not** override `GetAuthenticationAsync`, `ProvisionAsync`, `GetAsync`, `UpdateAsync`, `ReplaceAsync`, or `DeleteAsync` — all inherited from `RESTIntegrationV4`.
- **Call base first:** Always call `base.MapAndPreparePayloadAsync(...)` and post-process its result. Do **not** re-implement `JSONParserUtilV2` mapping logic or read `resource.KIExtension.ExtensionAttribute1` directly — the schema decides the target field name; the override only reshapes the value.
- **No new enum:** Do **not** add an `IntegrationMethods` value. Set `IntegrationMethod = IntegrationMethods.REST`; route by AppId.
- **No `dynamic` payload mutation:** normalize to `JObject` before manipulating.
- **Idempotent transform:** guard on `JTokenType.String` so re-processing an already-array node is a no-op.
- **Security:** Do not log the full payload or PII; log only the `reports` code count and `resource.Identifier`.
- **Coding standards:** file-scoped namespace, `using var` for disposables (none expected here), no `async void`, C# `StringSplitOptions.TrimEntries`.
- **Config over hardcode:** the split delimiter and target field name are `private const`s; if a second Bo field later needs the same treatment, generalize to a small list rather than copy-pasting.

---

## 🟩 PART 4: VERIFICATION & DEFINITION OF DONE

**Expected Output:**
`MapAndPreparePayloadAsync` returns a `JObject` identical to the base output **except** the `reports` node, which is converted from a comma-separated string to a primitive JSON string array:

```json
// base output
{ "userName": "jdoe", "reports": "PAC01A,PAC01B" }
// ASNBBoIntegration output
{ "userName": "jdoe", "reports": ["PAC01A", "PAC01B"] }
```

**Definition of Done:**
- [ ] `ASNBBoIntegration` created in `PNB/`, inherits `RESTIntegrationV4`, overrides only `MapAndPreparePayloadAsync`.
- [ ] CSV `ExtensionAttribute1` → `reports` array transform verified (happy path, single value, whitespace, empty, missing field, already-array).
- [ ] Registered in `ServiceExtension.cs`; AppId routing added in `appsettings.json`.
- [ ] Solution builds; all new unit tests green.
- [ ] Manual/integration check: a provisioning call to the ASNB Bo app sends `reports` as an array and returns success.

---

## ⬜ PART 5: IMPACT & DEPENDENCIES

**Impacted Components:**
- `KN.KloudIdentity.Mapper` — new file `PNB/ASNBBoIntegration.cs`; one `AddScoped` line in `ServiceExtension.cs`.
- `Microsoft.SCIM.WebHostSample` — one `AppIdToIntegration` entry in `appsettings.json`.
- `KN.KloudIdentity.MapperTests` — new test file `MapperCore/PNB/ASNBBoIntegrationTests.cs`.

**No changes to:**
- `RESTIntegrationV4` (base untouched — override only).
- `JSONParserUtilV2`, `ExtensionAttributeKIUserBase`, `IntegrationMethods` enum, `IntegrationBaseFactory`.
- Any inbound / Group provisioning code.

**Dependent Tasks:**
- MgtPortal app record for ASNB Bo must exist with its `AppId` before the `appsettings.json` mapping is finalized.
- Outbound schema mapping for this app must map `ExtensionAttribute1 → reports` as **String** (Direct).
- Entra attribute mapping must emit the report codes as a single comma-separated string into `ExtensionAttribute1` (e.g. `Constant "PAC01A,PAC01B"` — **not** `Split(...)`).

**Anti-Drift Log:**
- Using the schema's built-in `AttributeDataTypes.Array` was considered and rejected: `JSONParserUtilV2.MakeJsonArray` wraps a scalar CSV source as a single element (`["PAC01A,PAC01B"]`) instead of splitting — hence the code override.
- Sending the value as a JSON array from Entra was rejected: `ExtensionAttribute1` is a shared scalar `string` slot; changing it to a collection would break every other app that uses it.
