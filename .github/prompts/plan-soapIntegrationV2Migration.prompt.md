## 🟥 PART 1: ARCHITECTURAL CONTEXT & INTENT

**Introduction:**  
Move SOAP integration from legacy contract behavior to full V2 action-step behavior so SOAP can run through the same V4 orchestration model as REST. This removes current compile/runtime mismatch, enables consistent multi-step provisioning behavior, and prevents SOAP-specific branching drift.

**Endpoint & Inputs:**

- **Route:** SCIM user lifecycle operations already orchestrated by V4 user flows (Create/Get/Replace/Update/Delete).
- **Payload:** Core2EnterpriseUser, IPatch/PatchRequest2, and mapped SOAP XML payloads from attribute schemas/templates.
- **Auth/Permissions:** Existing AppConfig-based outbound auth (AuthenticationMethodOutbound + SOAP auth options + token/transport auth); no new permission surface.

**Architectural Boundaries:**

- **Target Service:** KloudIdentity SCIM Connector outbound integration layer.
- **Core Patterns:** Interface-driven integration contracts, factory-based integration resolution, action-step workflow orchestration in V4 user services.
- **Infrastructure:** HttpClientFactory SOAP calls, AppSettings-driven integration mappings, DI service registration and resolution.

---

## 🟨 PART 2: IMPLEMENTATION PHASES (MILESTONES)

_To prevent AI drift, this task must be executed in the following order. Each phase requires a build/test pass before moving to the next._

### Phase 1: Interface Conformance & Compile Stability

- **Logic:** Add all missing V2 SOAP method signatures (appId, actionStep, cancellationToken) and provide safe adapter implementations to existing SOAP behavior where appropriate.
- **Agent Instruction:** "Implement only V2 contract conformance in SOAPIntegration and compile fixes. Do not change orchestration behavior yet."
- **Checkpoint:** SOAPIntegration compiles as full IIntegrationBaseV2 with no missing-member diagnostics.

### Phase 2: DI/Factory Wiring for SOAP V2 Resolution

- **Logic:** Register SOAPIntegration as IIntegrationBaseV2 while keeping existing IIntegrationBase registration for backward compatibility.
- **Agent Instruction:** "Update DI/service wiring only; keep behavior unchanged."
- **Checkpoint:** IntegrationBaseFactory can resolve SOAP via its V2 integration list when mapping selects SOAP.

### Phase 3: V4 Caller Compatibility for SOAP Payload Mapping

- **Logic:** Update V4 caller paths that currently call MapAndPreparePayloadAsync(schema, resource) to call the appConfig overload where SOAP may be selected.
- **Agent Instruction:** "Change caller invocation shapes only; do not alter action-step order or branching semantics."
- **Checkpoint:** No SOAP execution path can hit the current NotSupportedException due to missing appConfig.

### Phase 4: ActionStep-Aware SOAP Semantics (Required)

- **Logic:** Implement strict actionStep-aware SOAP CRUD behavior: endpoint selection by step metadata, step-specific template/schema selection, and explicit operation/verb validation. Ensure ReplaceAsync returns Core2EnterpriseUser per V2.
- **Agent Instruction:** "Implement strict validation and fail fast when action-step metadata is incomplete or invalid."
- **Checkpoint:** SOAP V2 methods honor action-step config and reject invalid/missing required step metadata.

### Phase 5: Create Flow Alignment for Non-REST Integrations (In Scope)

- **Logic:** Update CreateUserV4 REST-only multi-step gate so SOAP can execute configured CREATE action steps.
- **Agent Instruction:** "Refactor create orchestration gating to include SOAP multi-step CREATE while preserving existing REST behavior."
- **Checkpoint:** SOAP CREATE action steps execute through V4 when configured.

### Phase 6: Test Hardening

- **Logic:** Add/adjust tests for V2 SOAP signatures, DI resolution, V4 call-path behavior, strict action-step validation, and SOAP fault/error/cancellation handling.
- **Agent Instruction:** "Write tests for regressions and edge cases, then finalize implementation to satisfy tests."
- **Checkpoint:** All relevant tests pass; no regressions in REST V4 flows.

---

## 🟦 PART 3: TECHNICAL CONSTRAINTS & GUARDRAILS

_Mandatory rules for the AI Agent to follow to ensure long-term maintainability._

- **Coding Standards:** Maintain existing project style and async conventions; keep backward-compatible V1 methods unless explicitly removed in a separate migration.
- **Security:** Keep XML hardening (XXE prevention), do not log secrets/tokens, validate action-step inputs before endpoint/template use.
- **Performance:** Continue HttpClientFactory reuse, avoid unnecessary auth/token fetches, avoid repeated template parsing in loops where possible.
- **Prohibited:** No manual HttpClient creation, no bypassing IntegrationBaseFactory, no silent fallback to first URI/template when action-step metadata is invalid (strict mode).

---

## 🟩 PART 4: VERIFICATION & DEFINITION OF DONE

**Expected Output:**

- SOAPIntegration fully satisfies IIntegrationBaseV2 and is resolvable through V4 orchestration paths.
- SOAP CRUD with action-step context behaves deterministically and ReplaceAsync returns expected user object.

**Unit Test Scenarios:**

- [ ] **Happy Path:** SOAP Create/Get/Replace/Update/Delete through V4 succeeds with expected identifiers/outcomes.
- [ ] **Validation:** Missing endpoint/template/action-step metadata fails with deterministic exceptions and clear diagnostics.
- [ ] **Resilience:** SOAP HTTP errors and SOAP Fault payloads are handled correctly; cancellation tokens are honored.

---

## ⬜ PART 5: IMPACT & DEPENDENCIES

- **Impacted Components:**  
  [KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/SOAPIntegration.cs](KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/SOAPIntegration.cs)  
  [KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/IIntegrationBaseV2.cs](KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/IIntegrationBaseV2.cs)  
  [KN.KloudIdentity.Mapper/Utils/ServiceExtension.cs](KN.KloudIdentity.Mapper/Utils/ServiceExtension.cs)  
  [KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/IntegrationBaseFactory.cs](KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/IntegrationBaseFactory.cs)  
  [KN.KloudIdentity.Mapper/MapperCore/User/CreateUserV4.cs](KN.KloudIdentity.Mapper/MapperCore/User/CreateUserV4.cs)  
  [KN.KloudIdentity.Mapper/MapperCore/User/GetUserV4.cs](KN.KloudIdentity.Mapper/MapperCore/User/GetUserV4.cs)  
  [KN.KloudIdentity.Mapper/MapperCore/User/ReplaceUserV4.cs](KN.KloudIdentity.Mapper/MapperCore/User/ReplaceUserV4.cs)  
  [KN.KloudIdentity.Mapper/MapperCore/User/UpdateUserV4.cs](KN.KloudIdentity.Mapper/MapperCore/User/UpdateUserV4.cs)  
  [KN.KloudIdentity.Mapper/MapperCore/User/DeleteUserV4.cs](KN.KloudIdentity.Mapper/MapperCore/User/DeleteUserV4.cs)  
  [KN.KloudIdentity.MapperTests/SOAPIntegration/SOAPIntegrationUnitTests.cs](KN.KloudIdentity.MapperTests/SOAPIntegration/SOAPIntegrationUnitTests.cs)

- **Dependent Tasks:**  
  Apply strict action-step validation policy for SOAP V2 methods (confirmed).  
  Include CREATE orchestration alignment so SOAP uses multi-step CREATE in this same migration (confirmed).  
  Ensure AppSettings integration mapping resolves SOAP implementation type names used by factory selection.

- **Anti-Drift Log:**  
  Current code shows a declared V2 SOAP class that does not implement required V2 signatures and is not registered as V2 in DI. Plan prioritizes contract/DI stabilization before semantic refactor.
