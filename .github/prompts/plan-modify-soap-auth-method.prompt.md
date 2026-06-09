---
name: Development Task (Plan-First)
about: Technical implementation plan for AI-assisted development
title: "[Dev Task] [SOAP Authentication] Modify Soap Authentication - Add SoapWsSecurity & SoapNtlm Values"
labels: "Dev-Task, Plan-Pending"
assignees: ""
---

## 🟥 PART 1: ARCHITECTURAL CONTEXT & INTENT

**Introduction:**
The SOAP authentication pipeline has two layers: an HTTP token path (`GetTokenListAsync` → `IAuthStrategy`) and a SOAP applier pipeline (`ISoapAuthApplier` implementations). SOAP-native auth mechanisms — WS-Security UsernameToken and NTLM transport auth — operate at the SOAP protocol level and do **not** produce HTTP tokens. Currently, no `AuthenticationMethods` enum values represent these mechanisms. If an `AuthenticationFlowStep` is configured with a SOAP-native method, `GetTokenListAsync` throws `AuthenticationException` because no `IAuthStrategy` is registered for it. The goal is to introduce explicit enum markers so these steps are correctly skipped in the token-production loop while still being processed by the applier pipeline.

**Endpoint & Inputs:**
* **Route:** Internal — no HTTP endpoint. Triggered via `SOAPIntegration.SendSoapRequestAsync` → `CreateHttpClientAsync` → `GetAuthenticationAsync`.
* **Payload:** `AppConfig.AuthenticationFlow.Steps[*].AuthenticationMethod` (enum value)
* **Auth/Permissions:** SOAP outbound integration configured via `AppConfig.AuthenticationFlow`

**Architectural Boundaries:**
* **Target Service:** `KN.KloudIdentity.Mapper` / `KN.KloudIdentity.Mapper.Domain`
* **Core Patterns:** Strategy Pattern (`IAuthStrategy`), Applier Pipeline (`ISoapAuthApplier`), Authentication Flow Steps
* **Infrastructure:** `AuthenticationFlow` stored in app configuration; credentials in `AuthenticationFlowStep.AuthenticationDetails` (deserialized as `SOAPAuthenticationOptions`)

---

## 🟨 PART 2: IMPLEMENTATION PHASES (MILESTONES)

*Each phase requires a build/test pass before moving to the next.*

---

### Phase 1: Domain Model — Add Enum Values

**File:** `KN.KloudIdentity.Mapper.Domain/Authentication/AuthenticationMethods.cs`

**Logic:** Add two new values at the end of the `AuthenticationMethods` enum. These act as markers — they signal that a flow step carries SOAP-level credentials and does not produce an HTTP token.

```csharp
SoapWsSecurity = 8,   // WS-Security UsernameToken — credentials injected into SOAP envelope
SoapNtlm = 9          // NTLM transport auth — credentials applied via HttpClientHandler
```

**Agent Instruction:** Add only the two enum values. No logic, no other file changes.

**Checkpoint:** Solution builds with 0 errors. No test failures.

---

### Phase 2: Core Auth Context Guard — `GetTokenListAsync`

**File:** `KN.KloudIdentity.Mapper/Auth/AuthContextV2.cs`

**Logic:** Inside the `foreach` loop, **before** the strategy lookup, add a `continue` guard for the two new SOAP-native methods. This prevents a throw when a flow step uses `SoapWsSecurity` or `SoapNtlm` — neither has a registered `IAuthStrategy` and neither is expected to produce a token.

```csharp
foreach (var step in flow.Steps.OrderBy(s => s.StepOrder))
{
    var method = step.AuthenticationMethod;

    if (method == AuthenticationMethods.SoapWsSecurity
        || method == AuthenticationMethods.SoapNtlm)
        continue;

    var strategy = _authStrategies.FirstOrDefault(x => x.AuthenticationMethod == method)
                   ?? throw new AuthenticationException($"Authentication method {method} is not supported.");

    var authDetails = step.AuthenticationDetails;
    var token = await strategy.GetTokenAsync(authDetails);

    if (string.IsNullOrWhiteSpace(token))
        throw new AuthenticationException($"Authentication step '{step.StepTitle}' failed to produce a token.");

    tokens[step.StepOrder] = token;
}
```

**Agent Instruction:** Add only the `continue` guard block. Do not alter any other logic in the method. The `throw` for genuinely unsupported methods must remain intact.

**Checkpoint:** Solution builds. No test failures.

---

### Phase 3: Integration Layer Guard — `ShouldResolveToken`

**File:** `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/SOAPIntegration.cs`

**Logic:** Extend the `None` check in `ShouldResolveToken` to also return `false` for `SoapWsSecurity` and `SoapNtlm`. This handles the edge case where `appConfig.AuthenticationMethodOutbound` is set to one of the SOAP-native values rather than `None`.

```csharp
// Replace:
if (authMethod != AuthenticationMethods.None)
    return true;

// With:
if (authMethod == AuthenticationMethods.None
    || authMethod == AuthenticationMethods.SoapWsSecurity
    || authMethod == AuthenticationMethods.SoapNtlm)
    return false;

return true;
```

**Agent Instruction:** Change only the guard condition in `ShouldResolveToken`. No other logic in `SOAPIntegration.cs` should change in this phase.

**Checkpoint:** Solution builds. No test failures.

---

### Phase 4: Test Coverage

**Files:**
* `KN.KloudIdentity.MapperTests/SOAPIntegration/SOAPIntegrationUnitTests.cs`
* `KN.KloudIdentity.MapperTests/AuthContextV2/AuthContextV2Tests.cs` (existing or new)

#### 4a — Update `CreateSoapFlowStep` helper in `SOAPIntegrationUnitTests`

Add a `method` parameter with default `AuthenticationMethods.SoapWsSecurity`:

```csharp
private static AuthenticationFlowStep CreateSoapFlowStep(
    dynamic authenticationDetails,
    int stepOrder = 1,
    AuthenticationMethods method = AuthenticationMethods.SoapWsSecurity) =>
    new()
    {
        StepTitle = "SOAP Auth",
        StepOrder = stepOrder,
        AuthenticationMethod = method,
        IsRequired = true,
        AuthenticationDetails = authenticationDetails
    };
```

Update the three existing flow step tests (`SOAPAuthOptions_ResolvedFromFlowStep_DirectShape`, `SOAPAuthOptions_ResolvedFromFlowStep_NestedKeyShape`, `SOAPAuthOptions_FlowStepTakesPriorityOver_AppConfigProperty`) to pass `method: AuthenticationMethods.SoapWsSecurity` explicitly.

#### 4b — New tests in `AuthContextV2Tests`

Add four new test cases covering the skip behaviour introduced in Phase 2:

| Test Name | Setup | Expected |
|---|---|---|
| `GetTokenListAsync_WithSoapWsSecurityStep_ReturnsEmptyDictionary` | Single step: `SoapWsSecurity` | Dictionary is empty; no throw |
| `GetTokenListAsync_WithSoapNtlmStep_ReturnsEmptyDictionary` | Single step: `SoapNtlm` | Dictionary is empty; no throw |
| `GetTokenListAsync_MixedFlow_BearerAndSoapWsSecurity_ReturnsBearerTokenOnly` | Step 1 = `Bearer` (mock returns `"bearer-token"`), Step 2 = `SoapWsSecurity` | Dictionary = `{ { 1, "bearer-token" } }` only |
| `GetTokenListAsync_AllStepsSoapNative_ReturnsEmptyDictionary_NoThrow` | Two steps: `SoapWsSecurity` + `SoapNtlm` | Empty dictionary; no exception |

**Agent Instruction:** Implement 4a and 4b. Do not modify any other existing tests.

**Checkpoint:** All existing 249 tests pass. All 4 new `AuthContextV2` tests pass.

---

## 🟦 PART 3: TECHNICAL CONSTRAINTS & GUARDRAILS

* **Coding Standards:** C# 12, file-scoped namespaces, record/class style consistent with existing domain models
* **Enum Integrity:** Never change existing enum integer values (`None = 0` through `DotRez = 7`). New values append only.
* **No New Strategies:** Do not create `IAuthStrategy` implementations for `SoapWsSecurity` or `SoapNtlm`. They produce no tokens and must not participate in the token loop.
* **Preserve Throw:** The `throw new AuthenticationException(...)` for genuinely unsupported methods in `GetTokenListAsync` must remain. Only `SoapWsSecurity` and `SoapNtlm` are skipped via `continue`.
* **Backward Compatibility:** All existing `AuthenticationMethod = None` SOAP configurations continue to work unchanged. The `ShouldResolveToken` guard for `None` stays in place; the new values are additive.
* **No Logic in Enum File:** Only enum values in `AuthenticationMethods.cs`. No extension methods, no helper classes in the same file.
* **No PII in Logs:** Credentials from `SOAPAuthenticationOptions` must never be logged.

---

## 🟩 PART 4: VERIFICATION & DEFINITION OF DONE

**Expected Behaviour After Implementation:**

| Scenario | `GetTokenListAsync` Result | `ShouldResolveToken` Result |
|---|---|---|
| Step with `SoapWsSecurity` only | Empty dictionary, no throw | `false` if app-level method is also `SoapWsSecurity` |
| Step with `SoapNtlm` only | Empty dictionary, no throw | `false` if app-level method is `SoapNtlm` |
| Mixed: `Bearer` step + `SoapWsSecurity` step | `{ { 1, "bearer-token" } }` | `true` (Bearer drives token resolution) |
| All SOAP-native steps | Empty dictionary, no throw | `false` |
| Unsupported method (not in enum) | Throws `AuthenticationException` | N/A |

**Unit Test Scenarios:**
* [ ] **Happy Path — WsSecurity only:** `GetTokenListAsync` with a single `SoapWsSecurity` step returns an empty dictionary without throwing.
* [ ] **Happy Path — NTLM only:** `GetTokenListAsync` with a single `SoapNtlm` step returns an empty dictionary without throwing.
* [ ] **Happy Path — Mixed flow:** `Bearer` step produces token; `SoapWsSecurity` step is skipped; dictionary contains only the Bearer token.
* [ ] **Happy Path — All SOAP-native:** No exception thrown; empty dictionary returned.
* [ ] **Regression — Existing SOAP tests:** All 249 existing tests continue to pass.
* [ ] **Regression — Flow step resolution tests:** Three existing flow step tests (`DirectShape`, `NestedKeyShape`, `Priority`) pass with `SoapWsSecurity` method explicitly set.
* [ ] **Guard — `ShouldResolveToken`:** `ProvisionAsync` with `authMethodOutbound = SoapWsSecurity` does not call `GetAuthenticationAsync`.

---

## ⬜ PART 5: IMPACT & DEPENDENCIES

**Impacted Components:**
* `KN.KloudIdentity.Mapper.Domain` — `AuthenticationMethods` enum (shared; any consumer that switches on all values needs awareness of new cases)
* `KN.KloudIdentity.Mapper` — `AuthContextV2`, `SOAPIntegration`
* `KN.KloudIdentity.MapperTests` — `SOAPIntegrationUnitTests`, `AuthContextV2Tests`

**Dependent Tasks (must be complete before this plan):**
* `plan-modify-soap-authentication-appconfig.md` — moves `SOAPAuthenticationOptions` into `AuthenticationFlow` steps ✅ Complete
* `CreateHttpClientAsync` fix — uses step-level `AuthenticationMethod` + `AuthenticationDetails` ✅ Complete

**What Does NOT Change:**
| Component | Reason |
|---|---|
| All `ISoapAuthApplier` implementations | Read from `SoapAuthContext`; unaffected by enum source |
| `ResolveSoapAuthenticationOptions` | Already reads from flow step `AuthenticationDetails` |
| `SoapAuthContext` | No schema change; `Token` remains `string?` |
| DI registrations | No new types registered |
| `CreateHttpClientAsync` | Already fixed to use step-level auth details |

**Anti-Drift Log:** *(To be filled during implementation if the AI suggests logic changes beyond the scope of this plan)*
