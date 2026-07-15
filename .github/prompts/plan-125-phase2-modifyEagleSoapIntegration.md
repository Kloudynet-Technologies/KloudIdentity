---
name: Development Task (Plan-First)
about: Technical implementation plan for AI-assisted development
title: "[Dev Task] [Eagle SOAP Integration] EagleSOAPIntegration - Phase 2: Functional Correctness"
labels: "Dev-Task, Plan-Pending"
assignees: ""
---

<!--
  Source: "NEW Eagle Integration Gap Analysis_full_20260713.htm" — Recommended Implementation Plan, Phase 2.
  Predecessor: plan-125-phase1-modifyEagleSoapIntegration.md (Milestones A–E complete, 281/281 tests green;
               Milestone F runbook pending config-owner execution).
  Branch: fix/125-eagle-integration (or successor branch after Phase 1 PR merges)
  Gap IDs covered: GAP-CRT-04, GAP-UPD-03, GAP-GET-03, GAP-DEL-02, GAP-CODE-03, GAP-CODE-06, GAP-CODE-07
-->

## 🟥 PART 1: ARCHITECTURAL CONTEXT & INTENT

**Introduction:**
> Phase 1 made one full Create → Read → Update → Delete cycle possible: synchronous
> `taskStatusResponse` validation, prefix-agnostic SOAP Fault detection, HTTP Basic authentication
> (config-only), a payload placeholder guard, and the corrected AppConfig. Phase 2 closes the
> **silent-drift behaviours** — cases where the integration works but quietly produces wrong state
> or wrong data over time:
>
> 1. **Disabled Entra users stay enabled in Eagle** — `<accountState>` is hardcoded `U`; the SCIM
>    `active` flag never reaches Eagle (GAP-CRT-04).
> 2. **Role revocations may never propagate** — the PATCH-path `UpdateAsync` sends without
>    `<processingOptions>REINSERT</processingOptions>`; Eagle's default CHANGE is merge-only, so
>    revoked roles are silently retained (GAP-UPD-03).
> 3. **Round-trip identity mismatch** — GET maps `emailAddress → UserName` while Create maps Entra
>    `UserName → userId`; Entra matching logic comparing userName can mismatch and re-trigger
>    creates (GAP-GET-03).
> 4. **Failed deletes report success** — the delete `taskAcknowledgement` means *accepted*, not
>    *completed*; an async "User does not exist" failure never reaches SCIM (GAP-DEL-02).
> 5. **No traceability into Eagle** — the Eagle correlationId GUID is generated but never logged,
>    so EJM tasks/load files can't be traced from KloudIdentity logs (GAP-CODE-06/07).
> 6. **V1 (non-ActionStep) overloads mislead on failure** — with empty `UserURIs` they throw a
>    confusing "endpoint not configured" instead of pointing at ActionStep configuration (GAP-CODE-03).

**Endpoint & Inputs:**
* **Route (outbound SOAP):** `POST https://pnb-d002-star.eagleaccess.com/EagleMLWebService20` (unchanged)
* **Route (outbound REST):** `GET https://pnb-d002-star.eagleaccess.com/eagle/v2/users` (unchanged; now also used post-delete for verification)
* **Payload:** EagleML `UserAdministrationTransactionMessage` — Create/Update templates gain the reserved `{{accountState}}` placeholder
* **Auth/Permissions:** HTTP Basic on both surfaces (Phase 1 decision — unchanged)

**Architectural Boundaries:**
* **Target Service:** `KN.KloudIdentity.Mapper` (`MapperCore/IntegrationMethods`)
* **Core Patterns:** unchanged — ActionStep dispatch, template + attribute mapping, reserved placeholders injected by the integration (`{{CorrelationId}}`, now `{{accountState}}`)
* **Infrastructure:** no new packages; no DB/schema changes; `AppConfig.Actions` (already on the domain model) is used to resolve the GET step for delete verification
* **Files in scope:**
  * `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/EagleSOAPIntegration.cs` (all production changes land here)
  * `KN.KloudIdentity.MapperTests/SOAPIntegration/EagleInvestment/EagleSOAPIntegrationTests.cs` (+ fixtures)
  * `SampleAppConfig_Corrected.json` (customer deliverable — template v2 with `{{accountState}}`)
  * **No changes** to `SOAPIntegration.cs`, `AuthContextV2.cs`, `SOAPParserUtil.cs`, or any strategy/applier.

---

## 🟨 PART 2: IMPLEMENTATION PHASES (MILESTONES)
*Execute strictly in order; each milestone gates on `dotnet test` (full suite) before the next. TDD: write the failing test(s) first inside each milestone.*

### Milestone A: GET round-trip mapping — GAP-GET-03
* **Logic:** Make Read return the same identity Create wrote. Eagle `userId` is the join key.
* **Steps:**
  1. Tests first (red): `GetAsync_MapsUserIdToUserName_NotEmail` (UserName == `userId`), `GetAsync_MapsEmailAddressIntoElectronicMailAddresses` (email surfaced as the email attribute, ItemType `work`).
  2. Change `ParseEagleRestUserResponse` in `EagleSOAPIntegration.cs`:
     * `Identifier = userId` (unchanged), **`UserName = userId`** (was `emailAddress`),
     * `DisplayName = userFullName` (unchanged),
     * `ElectronicMailAddresses = [ new ElectronicMailAddress { Value = emailAddress, ItemType = "work" } ]` when `emailAddress` is non-empty.
  3. Update the four existing tests that assert `UserName == email` (`GetAsync_WhenRestReturnsValidJson_ReturnsMappedUser`, `GetAsync_WithGoldenFixture_ReturnsMappedUser`, `GetAsyncV2_WithValidRestResponse_ReturnsMappedUser`, and the `EagleRestUserJson`-based variants) to the new mapping.
* **Agent Instruction:** "Mapping change only — do not touch URL building, auth, or 404 semantics. The golden fixture `eagle-rest-get-user.json` stays byte-identical; only expectations change."
* **Checkpoint:** Full suite green; new tests green; golden-fixture test asserts `UserName == "EAGLE_TEST_USER01"`.

### Milestone B: accountState from SCIM `active` — GAP-CRT-04
* **Logic:** Introduce `{{accountState}}` as the second **reserved, code-injected placeholder** (alongside `{{CorrelationId}}`): `resource.Active == true → "U"`, `false → "D"`. Injecting in code (not via attribute mapping) avoids extending `SOAPParserUtil` with value-transform semantics — prohibited scope this phase.
* **Steps:**
  1. Tests first: `MapAndPreparePayloadAsync_ActiveTrue_InjectsAccountStateU`, `MapAndPreparePayloadAsync_ActiveFalse_InjectsAccountStateD`, `MapAndPreparePayloadAsync_TemplateWithoutAccountStatePlaceholder_Unchanged` (hardcoded `<accountState>U</accountState>` templates keep working — backward compatible).
  2. In `MapAndPreparePayloadAsync`, alongside the `{{CorrelationId}}` injection:
     `injected = injected.Replace("{{accountState}}", resource.Active ? "U" : "D", StringComparison.Ordinal);`
     (Do NOT inject in the DELETE path — the delete EML has no accountState.)
  3. Update `CorrectedTemplate_CreateUser.xml` fixture and the golden-structure test resource to cover the placeholder (structure vs `AddUserRequest_Golden.xml` is unaffected — same element, different text).
  4. Config deliverable (applied in Milestone F): Create + Update templates in `SampleAppConfig_Corrected.json` change `<accountState>U</accountState>` → `<accountState>{{accountState}}</accountState>`.
  5. Document the reserved-placeholder list in the file header comment of `EagleSOAPIntegration.cs`: `{{CorrelationId}}`, `{{accountState}}` — attribute mappings must never use these as `DestinationField`.
* **Agent Instruction:** "Reserved-placeholder injection only. No `SOAPParserUtil` changes, no conditional-mapping engine work. Entra disable arrives as a SCIM update with `active=false` — the Update template carrying `{{accountState}}` is what makes disable-on-deprovision work; state that in the test comments."
* **Checkpoint:** Full suite green; a payload built from an `Active=false` resource contains `<accountState>D</accountState>`.

### Milestone C: REINSERT enforcement on the UPDATE path — GAP-UPD-03
* **Logic:** **Policy decision (from the gap analysis, to be confirmed at review):** UPDATE templates must carry `<processingOptions>REINSERT</processingOptions>`, same as REPLACE — the Postman-verified CHANGE uses REINSERT, and merge-only updates silently retain revoked roles. This *changes deliberate Phase-0 behavior* documented in the existing test `UpdateAsyncV2_WithoutProcessingOptions_HasNoReinsertRequirement`.
* **Steps:**
  1. Replace that test with `UpdateAsyncV2_WithoutReinsertProcessingOptions_ThrowsBeforeSendingSoapRequest` (mirror of the existing REPLACE guard test: throws, message contains `REINSERT`, `handler.Requests` empty).
  2. In `UpdateAsync` (ActionStep overload), call the existing `ValidateReinsertProcessingOption((string)payload, appConfig.AppId)` before `SendSoapRequestAsync` — identical to `ReplaceAsync`.
  3. Keep `ReplaceAsync` unchanged; the corrected Update template already contains REINSERT (Phase 1 config), so no config change is needed here.
* **Agent Instruction:** "This milestone intentionally inverts one existing test — delete it, don't weaken it. If the reviewer rejects the policy (merge-only PATCH wanted for some flows), STOP and record the alternative (config flag per ActionStep) in the Anti-Drift Log instead of implementing both."
* **Checkpoint:** Full suite green; an UPDATE payload without REINSERT never reaches the wire.

### Milestone D: Delete verification via REST GET — GAP-DEL-02
* **Logic:** A positive `taskAcknowledgement` only means *accepted*. After the ack, confirm the end state via the Extract Service: user absent ⇒ delete succeeded (also makes deletes of already-absent users idempotent successes — the desired end state holds).
* **Steps:**
  1. Tests first:
     * `DeleteAsync_WithAckAndUserGone_Succeeds` — SOAP ack positive, subsequent GET returns 404 → completes.
     * `DeleteAsync_WithAckButUserStillPresent_ThrowsAfterRetries` — GET keeps returning the user → throws with a message naming the userId and correlationIds.
     * `DeleteAsync_WhenNoGetActionStepConfigured_SkipsVerificationWithoutError` — ack-only behavior preserved (graceful degradation).
     * Handler note: the fake handler must route by URI (POST → ack XML, GET → REST JSON/404) — extend `TestHttpMessageHandler` usage with a request-aware response factory (already supported via `Func<HttpRequestMessage, HttpResponseMessage>`).
  2. In `DeleteAsync`, after `ValidateEagleResponse`:
     * Resolve the GET endpoint: `appConfig.Actions.FirstOrDefault(a => a.ActionName == ActionNames.GET)?.ActionSteps?.OrderBy(s => s.StepOrder).FirstOrDefault()?.EndPoint`, falling back to `UserURIs.Get`. If neither exists → `Log.Warning` (skip verification) and return.
     * Poll `FetchEagleUserViaRestAsync` expecting **absence**: treat `HttpResponseException(NotFound)` as success; if the user is still returned, retry up to **3 attempts** with `await Task.Delay(2000, cancellationToken)` between attempts (constants `DeleteVerifyMaxAttempts` / `DeleteVerifyDelayMs` — no magic numbers, no `Thread.Sleep`).
     * All attempts exhausted with the user still present → throw `InvalidOperationException` ("Eagle accepted the delete but the user is still returned by the Extract Service…", include userId + SCIM correlationId).
  3. Idempotency note (behavioural, no extra code): deleting a non-existent user yields a positive ack and a 404 on verification → reported as success to SCIM, which is the correct idempotent semantics per the vendor ("User does not exist" failure is Eagle-internal).
* **Agent Instruction:** "Reuse `FetchEagleUserViaRestAsync` — do not duplicate URL/auth logic. `Task.Delay` must observe the cancellationToken. Unit tests must not sleep: either assert on attempt counts via the handler's `Requests` list with delay constants overridable… if constants can't be injected cheaply, set `DeleteVerifyDelayMs` low (e.g. read from an internal static settable in tests via `InternalsVisibleTo` is NOT desired — prefer a protected virtual `DeleteVerificationPolicy` property the test subclass overrides)."
* **Checkpoint:** Full suite green and completes in seconds (no real 2s×3 sleeps in tests); delete of an existing-but-stuck user fails loudly.

### Milestone E: Traceability + V1-overload guard — GAP-CODE-06 / GAP-CODE-07 / GAP-CODE-03
* **Logic:** Make every Eagle task traceable from connector logs, and make the wrong-path failure modes say what to fix.
* **Steps:**
  1. **Correlation-pair logging (CODE-06):** the Eagle correlationId is injected in `MapAndPreparePayloadAsync`, but the SCIM correlationId isn't available there. Therefore log at send time: in each operation (`ProvisionAsync` V2, `ReplaceAsync`, `UpdateAsync`, `DeleteAsync`), extract the Eagle correlationId from the outbound payload (`FirstElementText`-style helper on the payload XML) and emit one `Log.Information` per operation: operation name, AppId, userId, SCIM correlationId, Eagle correlationId. After the response, log the Eagle `status`/`eagleStatId` at Information on success (the failure path already carries them in exceptions).
  2. **Zero-token error log (CODE-07):** in the Phase-1 `GetAuthenticationAsync` override, `Log.Error` the same message immediately before throwing (today it throws silently from the logs' perspective).
  3. **V1-overload guard (CODE-03):** first *investigate* — grep the outbound pipeline (`MapperCore/User`, `Outbound`, consumers) for calls to the non-ActionStep `ProvisionAsync`/`GetAsync` with `IntegrationMethods.SOAPEagle`. Record the finding in the Anti-Drift Log. Then, keep the V1 overloads functional (unit tests use them heavily; removal is not worth the churn) but replace the misleading `"Eagle WSDL endpoint not configured."` / `"Eagle REST GET URI not configured."` exceptions with messages that name both options: configure `UserURIs` **or** route through the ActionStep overloads (the normal path for `IntegrationMethodOutbound = 8`).
  4. Tests: assert the improved exception messages; log assertions are NOT required (Serilog static — don't build a log-capture harness for this).
* **Agent Instruction:** "No secrets, no full payloads/response bodies in the new log lines — identifiers only (AppId, userId, correlationIds, status, eagleStatId). Do not switch to IKloudIdentityLogger in this phase (CODE-08 is Phase 3)."
* **Checkpoint:** Full suite green; a manual run of any Eagle test with console Serilog shows the correlation pair.

### Milestone F: Config v2, regression, and dev-host verification addendum
* **Logic:** Ship the config delta and prove the four behaviour changes live.
* **Steps:**
  1. Update `SampleAppConfig_Corrected.json` (customer folder): Create + Update templates with `<accountState>{{accountState}}</accountState>` (only change — Milestones A/C/D/E are code-side).
  2. Full `dotnet test` + build in Release.
  3. Append a **Phase 2 addendum** to `Phase1_MilestoneF_Verification_Runbook.md` (or a sibling `Phase2_Verification_Runbook.md`) with four live checks:
     * **Disable:** set the Entra test user `active=false` → Eagle shows Account Disabled (`accountState=D`); re-enable → `U`.
     * **Role revocation:** remove a role-driving change and update → REINSERT resets the role set (verify in Eagle User Admin).
     * **Delete verification:** delete the user → SCIM success only after the Extract Service stops returning it; delete again (non-existent) → SCIM success (idempotent).
     * **Traceability:** for one operation, follow the logged Eagle correlationId to the EJM task and load file.
* **Checkpoint:** Full suite green; runbook addendum executed and archived → Phase 2 Definition of Done; Phase 3 planning may start.

---

## 🟦 PART 3: TECHNICAL CONSTRAINTS & GUARDRAILS

* **Coding Standards:** .NET 8, match `EagleSOAPIntegration.cs` style; XML parsing with `XmlDocument { XmlResolver = null }` and `local-name()` XPath; reserved placeholders replaced with `StringComparison.Ordinal`.
* **Scope discipline:** Phase 2 only — do NOT implement dynamic groups / repeating template blocks (TPL-03), save-time template validation (TPL-04), `{{UtcNow}}`, configurable streamName/outputFormat, per-request headers, `IKloudIdentityLogger` adoption, or PII-trimming of legacy messages (all Phase 3).
* **Base-class safety:** **zero changes** to `SOAPIntegration.cs`, `AuthContextV2.cs`, `SOAPParserUtil.cs`, appliers, and strategies this phase. All production edits live in `EagleSOAPIntegration.cs`.
* **Async & timing:** no `Thread.Sleep` anywhere; `Task.Delay` must take the cancellationToken; delete-verification attempts/delay are named constants with a test-overridable seam; unit tests must run in milliseconds, not real polling time.
* **Security/PII:** new log lines and exception messages carry identifiers only (AppId, userId, correlationIds, status codes) — never credentials, tokens, or full payload/response bodies.
* **Prohibited:** no new NuGet packages; no changes to placeholder syntax in `SOAPParserUtil`; no swallowing of verification failures; no weakening of Phase 1 guards (placeholder guard, response validation, no-token tripwire).

---

## 🟩 PART 4: VERIFICATION & DEFINITION OF DONE

**Expected Output:**
* GET returns `UserName = <Eagle userId>` with the email in `ElectronicMailAddresses` — Create/GET round-trip is identity-consistent.
* A SCIM update with `active=false` produces `<accountState>D</accountState>` in the outbound EML (and `U` on re-enable).
* An UPDATE payload without `<processingOptions>REINSERT</processingOptions>` throws before sending.
* `DeleteAsync` reports success only after the Extract Service confirms absence (or when no GET step is configured — logged skip); a stuck delete throws with actionable detail.
* Connector logs show `operation / AppId / userId / SCIM correlationId / Eagle correlationId (+ status, eagleStatId on success)` for every Eagle SOAP operation.

**Unit Test Scenarios:**
* [ ] **Round-trip:** GET maps `userId → UserName`; email in `ElectronicMailAddresses`; golden REST fixture re-asserted; 404 semantics untouched.
* [ ] **accountState:** `Active=true → U`, `Active=false → D`; template without the placeholder unchanged; DELETE path never injects it; golden request-structure test still passes.
* [ ] **REINSERT:** UPDATE without REINSERT throws before send (handler saw zero requests); with REINSERT succeeds; REPLACE behavior unchanged.
* [ ] **Delete verification:** ack + GET 404 → success; ack + user still present after max attempts → throws; no GET step configured → success with skip; already-absent user → idempotent success.
* [ ] **Guards & messages:** V1 overloads throw messages naming both `UserURIs` and the ActionStep path; zero-token tripwire unchanged and still green.
* [ ] **Regression:** full `dotnet test` suite green; Phase 1 golden fixtures untouched byte-for-byte.

**Definition of Done:** all boxes checked + Milestone F live addendum executed on the PNB dev host and archived + PR referencing issue #125 lists the Phase 2 gap IDs as closed.

---

## ⬜ PART 5: IMPACT & DEPENDENCIES

* **Impacted Components:**
  * `EagleSOAPIntegration` (sole production file: REST mapping, reserved placeholder, REINSERT guard, delete verification, logging, error messages)
  * Eagle test class + fixtures (one fixture text change: `CorrectedTemplate_CreateUser.xml` accountState placeholder)
  * Customer AppConfig `idw-eagle-dev` (Create/Update template text only)
* **Dependent Tasks / Preconditions:**
  * **Phase 1 Milestone F runbook must PASS on the dev host before Phase 2 merges** (Phase 2's delete verification and accountState checks build on a working cycle).
  * **Decision needed at plan review (blocker for Milestone C):** confirm the REINSERT-on-UPDATE policy — it makes KloudIdentity authoritative for Eagle role sets on every update (manually granted Eagle roles are reset). The alternative (per-ActionStep opt-out flag) goes to the Anti-Drift Log if chosen.
  * **Decision needed (Milestone D):** delete-verification retry policy defaults (proposed: 3 attempts × 2 s) — confirm against observed Eagle EJM processing latency during the Phase 1 runbook.
  * Phase 3 (dynamic groups, save-time validation, logging platform adoption, remaining Low items) starts only after the Phase 2 addendum passes.
* **Anti-Drift Log:** (append during implementation)
  * **Milestone A (14 Jul 2026):** GET mapping changed to `UserName = userId`, email → `ElectronicMailAddresses[work]`. Three existing GET tests updated to the new mapping (email now asserted via `ElectronicMailAddresses`, not `UserName`). Golden REST fixture untouched. `UserName` and `Identifier` now carry the same Eagle `userId` — intended (Eagle has a single user key).
  * **Milestone C (14 Jul 2026):** REINSERT-on-UPDATE policy adopted (plan's recommended default; user gave go-ahead to implement C+D). `UpdateAsync` now calls the shared `ValidateReinsertProcessingOption`. Inverted `UpdateAsyncV2_WithoutProcessingOptions_HasNoReinsertRequirement` → `..._ThrowsBeforeSendingSoapRequest` and added `..._WithReinsertProcessingOptions_CompletesSuccessfully`. **Two additional pre-existing UPDATE tests** (`UpdateAsyncV2_WithValidAck_CompletesWithoutException`, `UpdateAsync_EmlBodyContainsActionChange_FromTemplate`) required REINSERT added to their payloads — expected fallout, not in the plan's test list. Shared validation message generalized from "REPLACE" to "REPLACE/UPDATE". **REVERSIBLE:** if merge-only PATCH is wanted for some flows, replace the unconditional guard with a per-ActionStep opt-out flag.
  * **Milestone D (14 Jul 2026):** Delete verification added with defaults **3 attempts × 2000 ms** (plan's proposed default). Retry delay is a `protected virtual DeleteVerifyDelayMs` overridden to 0 by a test-only `NoDelayEagleSOAPIntegration` subclass (no `InternalsVisibleTo`, no real sleeps in tests). GET endpoint resolved from `AppConfig.Actions[GET]` then `UserURIs.Get`; absent → warning + skip. Two existing delete tests (`DeleteAsyncV2_WithValidAck...`, `DeleteAsync_EmlBodyContainsActionDelete...`) switched to method-aware handlers (POST→ack, GET→404) and the latter now asserts on the POST request body via `handler.Requests`. **CONFIRM at review:** the 2 s × 3 defaults against observed EJM latency during the Phase 1 Milestone F run.
  * **Serilog using:** added `using Serilog;` to `EagleSOAPIntegration.cs` for the delete-verification skip warning (Milestone D). Full logging suite is still Milestone E.
  * **Milestone E investigation (CODE-03):** confirmed the V4 pipeline routes SOAPEagle through the ActionStep overloads (`CreateUserV4:98` → `ProvisionAsync(payload, appId, appConfig, step, ...)`; `GetUserV4:72` → `GetAsync(identifier, appConfig, actionStep, ...)`). The V1 (non-ActionStep) overloads are a legacy/REST fallback — kept functional (tests use them), only their "not configured" error messages improved to name both `UserURIs` and the ActionStep path. No pipeline routing changes.
  * **Milestone E (14 Jul 2026):** Added `LogEagleRequest` (SCIM↔Eagle correlation pair, identifiers only — no payload body/PII) + `LogEagleSuccess` (status/eagleStatId) to PROVISION/REPLACE/UPDATE/DELETE; `Log.Error` before the zero-token throw. 2 new tests assert the improved V1 error messages. Did NOT adopt `IKloudIdentityLogger` (CODE-08 is Phase 3) and did NOT add a log-capture harness (Serilog static — assert on behavior/messages only), per plan.
  * **Milestone F (14 Jul 2026):** Config `{{accountState}}` delta already applied in Milestone B. Release build of `KN.KloudIdentity.Mapper` succeeds (0 errors; 170 pre-existing project-wide warnings). Live verification delivered as `Phase2_Verification_Runbook.md` (8 checks) for the config owner — cannot run from the dev workstation. Full suite 292/292 green.
