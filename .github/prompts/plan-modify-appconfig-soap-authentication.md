# Plan: Move SOAPAuthenticationOptions from AppConfig to AuthenticationFlow Steps

## Background

Currently `SOAPAuthenticationOptions` is a typed property on `AppConfig` (`AppConfig.SOAPAuthenticationOptions`).
The goal is to move it into `AuthenticationFlow` steps so that SOAP authentication credentials are passed
through `AuthenticationFlowStep.AuthenticationDetails` (dynamic type), consistent with how other
authentication methods are handled via the flow.

For SOAP integrations, `AppConfig.AuthenticationDetails` will be **null** — all authentication
configuration will arrive exclusively through `AuthenticationFlow.Steps[*].AuthenticationDetails`.

---

## Files Affected

| File | Change Type |
|---|---|
| `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/SOAPIntegration.cs` | Primary logic change |
| `KN.KloudIdentity.Mapper.Domain/Application/AppConfig.cs` | Mark property obsolete |
| `KN.KloudIdentity.MapperTests/SOAPIntegration/SOAPIntegrationUnitTests.cs` | New test cases |

---

## Step 1 — Add a Direct-Deserialization Helper

**File:** `SOAPIntegration.cs`

Add a new private static helper method:

```csharp
private static bool TryDeserializeSoapAuthDirectly(JsonElement root, out SOAPAuthenticationOptions? options)
```

- Calls `root.Deserialize<SOAPAuthenticationOptions>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })`
- Accepts the result only if at least one section is non-null (`Transport`, `WsSecurity`, or `TokenPlacement`)
- Returns `true` and populates `options` on success; `false` otherwise

**Why:** The existing `TryReadSoapAuthOptions` looks for a named property key inside a wrapper object.
A flow step's `AuthenticationDetails` may be the `SOAPAuthenticationOptions` object directly (no wrapper),
so a direct deserialization path is required.

---

## Step 2 — Add a Shared Extraction Helper for `dynamic` Auth Details

**File:** `SOAPIntegration.cs`

Add a new private static helper method:

```csharp
private static bool TryExtractSoapAuthFromDetails(dynamic authDetails, out SOAPAuthenticationOptions? options)
```

Logic (in order):
1. Serialize `authDetails` to a JSON string
2. Parse as `JsonDocument`
3. Attempt **direct** deserialization via Step 1 helper → return if valid
4. Attempt **nested key** lookup via existing `TryReadSoapAuthOptions` with all known key variants:
   `"SOAPAuthenticationOptions"`, `"SoapAuthenticationOptions"`, `"soapAuthenticationOptions"`, `"soapAuthOptions"`
5. Return `true` and populate `options` on first success; `false` if all attempts fail

---

## Step 3 — Rewrite `ResolveSoapAuthenticationOptions`

**File:** `SOAPIntegration.cs`

**Remove** the existing `appConfig.AuthenticationDetails` fallback block entirely.
It will always be null for SOAP integrations and is no longer part of the resolution chain.

**New resolution order:**

| Priority | Source | Notes |
|---|---|---|
| 1 | `AuthenticationFlow.Steps[*].AuthenticationDetails` | Primary — walk steps in `StepOrder` ascending, return first match |
| 2 | `AppConfig.SOAPAuthenticationOptions` | Backward compat — existing typed property, used until fully migrated |

Implementation for priority-1:

```csharp
var steps = appConfig.AuthenticationFlow?.Steps;
if (steps != null)
{
    foreach (var step in steps.OrderBy(s => s.StepOrder))
    {
        if (step.AuthenticationDetails == null) continue;
        if (TryExtractSoapAuthFromDetails(step.AuthenticationDetails, out var stepOptions))
            return stepOptions;
    }
}
```

---

## Step 4 — Mark `AppConfig.SOAPAuthenticationOptions` as Obsolete

**File:** `AppConfig.cs`

```csharp
[Obsolete("Configure SOAPAuthenticationOptions via AuthenticationFlow step AuthenticationDetails instead.")]
public SOAPAuthenticationOptions? SOAPAuthenticationOptions { get; set; }
```

Do **not** remove the property yet. Existing stored configurations that still carry the typed property
continue to work via the priority-2 fallback path. Removal is a follow-up task once all consumers
have been migrated to flow steps.

---

## Step 5 — Update Tests

**File:** `SOAPIntegrationUnitTests.cs`

Existing tests pass `SOAPAuthenticationOptions` via `AppConfig.SOAPAuthenticationOptions` directly.
They continue to pass unchanged via the priority-2 fallback path — no modification required.

Add three new test cases to cover the priority-1 path:

### Test 1 — Direct shape
`SOAPAuthOptions_ResolvedFromFlowStep_DirectShape`
- `AuthenticationDetails` is a `SOAPAuthenticationOptions` JSON object at the root (no wrapper key)
- Asserts the correct `SOAPAuthenticationOptions` is resolved

### Test 2 — Nested key shape
`SOAPAuthOptions_ResolvedFromFlowStep_NestedKeyShape`
- `AuthenticationDetails` wraps the options under `"SOAPAuthenticationOptions"` key
- Asserts the correct `SOAPAuthenticationOptions` is resolved

### Test 3 — Flow step takes priority
`SOAPAuthOptions_FlowStepTakesPriorityOver_AppConfigProperty`
- Both `AuthenticationFlow.Steps[0].AuthenticationDetails` and `AppConfig.SOAPAuthenticationOptions` are set with different values
- Asserts that the flow step value wins (priority-1)

---

## What Does NOT Change

| Component | Reason |
|---|---|
| `SoapAuthContext.AuthOptions` | Already `SOAPAuthenticationOptions?`; how the value is resolved is transparent to the appliers |
| All `ISoapAuthApplier` implementations | Read from `SoapAuthContext.AuthOptions`; source of that value is irrelevant |
| `AuthContextV2` / `GetTokenListAsync` | No enum changes in this step; no new strategies; untouched |
| `AuthenticationFlowStep` model | `AuthenticationDetails` is already `dynamic`; no schema change needed |
| DI registrations | No new types registered |

---