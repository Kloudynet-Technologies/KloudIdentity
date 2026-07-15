---
name: Development Task (Plan-First)
about: Technical implementation plan for AI-assisted development
title: "[Dev Task] [Eagle SOAP Integration] EagleSOAPIntegration - Phase 1: Unblock End-to-End CRUD Flow"
labels: "Dev-Task, Plan-Pending"
assignees: ""
---

<!--
  Source: "NEW Eagle Integration Gap Analysis_20260713.html" (13 Jul 2026) — Recommended Implementation Plan, Phase 1.
  Branch: fix/125-eagle-integration
  Gap IDs covered: GAP-CODE-01, GAP-CODE-02, GAP-CRT-01, GAP-UPD-01, GAP-AUTH-01, GAP-AUTH-02, GAP-AUTH-03,
                   GAP-CRT-02, GAP-CRT-03, GAP-UPD-02, GAP-DEL-01, GAP-GET-02, GAP-TEST-01/02/03
  Decision 2026-07-13: authentication = plain HTTP Basic on both SOAP and REST (vendor-documented mechanism);
                       WS-Security is NOT used. See Milestone D.
-->

## 🟥 PART 1: ARCHITECTURAL CONTEXT & INTENT

**Introduction:**
> The PNB IDW Eagle integration provisions Entra ID users into Eagle Investment Systems via EagleML SOAP
> (`/EagleMLWebService20`) and reads them back via the Eagle REST Extract Service (`/eagle/v2/users`).
> The 2026-07-13 gap analysis found that **no CRUD operation currently completes a successful round-trip**
> because of three defect clusters:
>
> 1. **Silent success on failure** — Postman-verified ADD/CHANGE calls return `taskStatusResponse`
>    (`status` / `severityCode` / `failedRecords`), but `CheckEagleAck` only inspects `isNegative`
>    (which exists only in the async `taskAcknowledgement`). A `FAILURE` status is treated as success.
>    SOAP Faults using Eagle's `soapenv:` prefix also bypass the `"<soap:Fault"` string check.
> 2. **Credentials never reach Eagle** — the AppConfig stores flat `{Username, Password, IncludeTimestamp}`
>    auth details, but `ResolveSoapAuthenticationOptions` only accepts a structured `SOAPAuthenticationOptions`
>    shape, so WS-Security injection silently no-ops. The REST GET path throws before calling Eagle because
>    `GetTokenListAsync` skips `SoapWsSecurity` steps and returns an empty token dictionary.
>    **Resolution decision:** drop WS-Security entirely and switch the flow to plain HTTP **Basic** authentication
>    (the vendor-documented mechanism for both surfaces) — a configuration change; see Milestone D.
> 3. **Templates and mappings don't line up** — template placeholders (`{{userId}}`, `{email}`, `{GUID}`)
>    never match the mapping `DestinationField` values (`urn:kn:ki:schema:userId`), the Delete step has no
>    `Identifier` mapping (hard validation failure), and the GET endpoint embeds a raw `{{userId}}` query that
>    the code then duplicates.
>
> Phase 1 fixes exactly these blockers — nothing more — so one full Create → Read → Update → Delete cycle
> succeeds against the PNB dev host with the same data as the working Postman captures.

**Endpoint & Inputs:**
* **Route (outbound SOAP):** `POST https://pnb-d002-star.eagleaccess.com/EagleMLWebService20` (EML `runTaskRequest`, from ActionStep `EndPoint`)
* **Route (outbound REST):** `GET https://pnb-d002-star.eagleaccess.com/eagle/v2/users?userid=…&streamName=eagle_ml-2-0_default_out_extract_service&outputFormat=json`
* **Payload:** EagleML `UserAdministrationTransactionMessage` built by `SOAPParserUtil<Core2EnterpriseUser>.BuildPayload` from ActionStep templates
* **Auth/Permissions:** Username/Password only — plain HTTP Basic (`Authorization: Basic base64(user:pass)`) on both the SOAP POST and the REST GET; no SOAP-level (WS-Security/NTLM) authentication

**Architectural Boundaries:**
* **Target Service:** `KN.KloudIdentity.Mapper` (`MapperCore/IntegrationMethods`)
* **Core Patterns:** ActionStep-driven integration dispatch (`IIntegrationBaseV2`), `ISoapAuthApplier` pipeline, template + attribute-mapping payload construction
* **Infrastructure:** `IHttpClientFactory` clients; no new packages, no schema/DB changes
* **Files in scope:**
  * `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/EagleSOAPIntegration.cs`
  * `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/SOAPIntegration.cs` (shared base — minimal, additive changes only)
  * `KN.KloudIdentity.MapperTests/SOAPIntegration/EagleInvestment/EagleSOAPIntegrationTests.cs`
  * `KN.KloudIdentity.MapperTests/SOAPIntegration/EagleInvestment/Fixtures/*.xml` (new golden fixtures)
  * `SampleAppConfig.json` (customer configuration deliverable — corrected templates + mappings; not a repo code file)

---

## 🟨 PART 2: IMPLEMENTATION PHASES (MILESTONES)
*To prevent AI drift, this task must be executed in the following order. Each milestone requires a build/test pass (`dotnet test KN.KloudIdentity.MapperTests/KN.KloudIdentity.MapperTests.csproj --filter "FullyQualifiedName~EagleSOAPIntegrationTests"`) before moving to the next.*

### Milestone A: Golden fixtures + failing tests (TDD baseline) — GAP-TEST-01/03
* **Logic:** Capture the real Eagle response shapes as test fixtures before touching production code.
* **Steps:**
  1. Add fixture files under `KN.KloudIdentity.MapperTests/SOAPIntegration/EagleInvestment/Fixtures/`:
     * `TaskStatusResponse_Add_Success.xml` — verbatim from `ADD_USER_REQUEST_RESPONSE.txt` (status `SUCCESS`, `severityCode 0`, `failedRecords 0`).
     * `TaskStatusResponse_Change_Success.xml` — verbatim from `CHANGE_USER_REQUEST_RESPONSE.txt`.
     * `TaskStatusResponse_Failure.xml` — copy of the ADD response with `<eag1:status>FAILURE</eag1:status>` and `<eag1:severityCode>2</eag1:severityCode>`.
     * `TaskStatusResponse_FailedRecords.xml` — copy with `status SUCCESS` but `<eag1:failedRecords>1</eag1:failedRecords>`.
     * `TaskAcknowledgement_Positive.xml` / `TaskAcknowledgement_Negative.xml` — from `DELETE_USER_REQUEST_RESPONSE.txt` (`isNegative` false / true).
     * `SoapFault_SoapenvPrefix.xml` — a SOAP 1.1 fault using the `soapenv:` namespace prefix inside an HTTP-200 body.
  2. Mark fixtures as `CopyToOutputDirectory` content in `KN.KloudIdentity.MapperTests.csproj` (follow the existing Fixtures include pattern).
  3. Write the new tests (they MUST fail at this point):
     * `ProvisionAsync_WithTaskStatusSuccess_Succeeds` (fixture 1 → returns identifier, no throw)
     * `ProvisionAsync_WithTaskStatusFailure_Throws` (fixture 3 → `InvalidOperationException` containing `FAILURE`)
     * `ProvisionAsync_WithFailedRecords_Throws` (fixture 4 → exception message contains `failedRecords`)
     * `UpdateAsyncV2_WithTaskStatusFailure_Throws` (fixture 3 via UPDATE path)
     * `ProvisionAsync_WithSoapenvFaultAndHttp200_Throws` (fixture 7 → `HttpRequestException`)
* **Agent Instruction:** "Create only fixtures and tests. Do not modify any production code in this milestone. Confirm the 5 new tests fail for the documented reason (silent success / missed fault) and all existing tests still pass."
* **Checkpoint:** Solution builds; existing Eagle tests green; the 5 new tests red with assertion failures (not setup errors).

### Milestone B: Eagle response validation — GAP-CODE-01 / GAP-CRT-01 / GAP-UPD-01
* **Logic:** Replace the `isNegative`-only check with response-shape-aware validation in `EagleSOAPIntegration.cs`.
* **Steps:**
  1. Rename the private `CheckEagleAck(string responseBody, string appId)` to `ValidateEagleResponse(string responseBody, string appId)` and update all 5 call sites (`ProvisionAsync` ×2, `ReplaceAsync`, `UpdateAsync`, `DeleteAsync`, `ExtractIdentifierFromSoapResponse`).
  2. Inside `ValidateEagleResponse`, detect the response shape with prefix-agnostic XPath (`XmlDocument { XmlResolver = null }`):
     * `//*[local-name()='taskAcknowledgement']` present → existing behavior: throw when `isNegative == "true"`.
     * `//*[local-name()='taskStatusResponse']` present → new rules, all must hold or throw `InvalidOperationException`:
       * `//*[local-name()='status']` inner text equals `SUCCESS` (OrdinalIgnoreCase);
       * every `//*[local-name()='failedRecords']` node parses to `0`;
       * include in the exception message: AppId, status, `severityCode`, first `correlationId`, and `eagleStatId` when present (no full response body — trimmed detail only).
     * Neither element found → throw `InvalidOperationException("Unrecognized Eagle response …")` — never default to success.
  3. Keep `ParseSoapUserResponse` behavior unchanged (it is the public override used by base-class flows); route it through the same shape detection so a `taskStatusResponse` with `FAILURE` also throws there.
* **Agent Instruction:** "Modify only `EagleSOAPIntegration.cs`. Do not change the base `SOAPIntegration` in this milestone. Make the Milestone A status-response tests pass."
* **Checkpoint:** All Milestone A tests green except `ProvisionAsync_WithSoapenvFaultAndHttp200_Throws`; no existing test broken.

### Milestone C: Prefix-agnostic SOAP Fault detection — GAP-CODE-02
* **Logic:** `SOAPIntegration.SendSoapRequestAsync` currently matches only the literal `"<soap:Fault"`. Eagle (and most stacks) use other prefixes.
* **Steps:**
  1. In `SOAPIntegration.SendSoapRequestAsync`, replace the `responseBody.Contains("<soap:Fault", …)` check with a compiled regex: `<(\w+:)?Fault[\s>/]` (case-insensitive) — this is additive (strictly detects more faults) and safe for the generic SOAP integration and its existing tests.
  2. Keep the existing log + `HttpRequestException` behavior unchanged.
* **Agent Instruction:** "This is the only permitted change to `SOAPIntegration.cs` in this milestone. Run the FULL test suite (all classes), not just the Eagle tests, to prove no regression in the generic SOAP integration."
* **Checkpoint:** `ProvisionAsync_WithSoapenvFaultAndHttp200_Throws` green; full `dotnet test` run green.

### Milestone D: Authentication via HTTP Basic (no SOAP-level auth) — GAP-AUTH-01 / GAP-AUTH-02 / GAP-AUTH-03 / GAP-TEST-02
* **Logic:** **Decision (13 Jul 2026): use plain HTTP Basic authentication for both surfaces** — the mechanism the vendor document demonstrates for the SOAP webservice and the REST Extract Service. No WS-Security, no `SOAPAuthenticationOptions`. The existing pipeline already supports this end-to-end: a `Basic` flow step is processed by `AuthContextV2.GetTokenListAsync` (only `SoapWsSecurity`/`SoapNtlm` are skipped), `BasicAuthStrategy` resolves the password from Key Vault (`KeyVaultReference` + `EncryptedData.IV` + app `EncryptionKey`), returns `base64(user:pass)`, and `HttpClientExtensions.SetAuthenticationHeaders` sets `Authorization: Basic <token>` on the client used for both the SOAP POST and the REST GET. **This milestone is therefore configuration + tests; no production code changes are expected.** GAP-AUTH-01/02 dissolve (the WS-Security resolution path is simply no longer used) and GAP-AUTH-03 is resolved by adopting the vendor-documented mechanism.
* **Steps:**
  1. **Configuration change (deliverable, applied with Milestone E's config work):**
     * `AuthenticationMethodOutbound`: `8` (SoapWsSecurity) → `1` (Basic).
     * Replace the `WSSecurity` flow step with a step `AuthenticationMethod: 1` whose `AuthenticationDetails` follow the `BasicAuthentication` shape: `Username`, `KeyVaultReference`, `EncryptedData.IV` (+ password stored encrypted in Key Vault). The credential MUST be saved through the portal's standard Basic-auth flow so the Key Vault secret and IV are generated correctly — do not hand-edit these fields.
  2. **Sanity check on the SOAP path (read-only code review, no changes):** confirm `ShouldResolveToken` returns true for `AuthenticationMethodOutbound = Basic`, that the WS-Security/transport appliers no-op cleanly when no `SOAPAuthenticationOptions` resolve (this is now the desired behavior, not a defect), and that `EagleSoapActionApplier` still sets the SOAPAction header. Record findings in the Anti-Drift Log.
  3. New tests (Eagle test class, flow step configured as Basic with a stubbed `IAuthContext` returning a known base64 token):
     * `ProvisionAsync_WithBasicAuthFlow_SendsBasicAuthorizationHeaderOnSoapPost` — capture the outgoing SOAP POST via the fake handler; assert `Authorization: Basic <token>` and that the envelope contains **no** `wsse:Security` element.
     * `GetAsync_WithBasicAuthFlow_SendsBasicAuthorizationHeader` — assert `Authorization: Basic <token>` on the REST GET and no "no token was resolved" exception.
     * `ProvisionAsync_WhenAuthFlowResolvesNoToken_Throws` — empty token dictionary with a configured flow → clear exception before the HTTP call (guards against a silent unauthenticated send if the config regresses).
* **Agent Instruction:** "Do not add WS-Security fallbacks, transport-Basic options, or any change to `SOAPIntegration`/`AuthContextV2`/`BasicAuthStrategy` in this milestone — the Basic path already works; prove it with tests. If step 2 or the new tests reveal the Basic path does NOT work as analyzed, STOP and record the finding in the Anti-Drift Log before writing any fix."
* **Checkpoint:** 3 new auth tests green; full suite green; grep confirms no password/secret value is interpolated into any log/exception string.

### Milestone E: Payload safety guard + AppConfig repair — GAP-CRT-02/03, GAP-UPD-02, GAP-DEL-01, GAP-GET-02
* **Logic:** One code guard (generic unresolved-placeholder check) plus the corrected customer configuration artifact.
* **Steps:**
  1. **Code guard** — in `EagleSOAPIntegration.MapAndPreparePayloadAsync` (and the DELETE payload build), after `BuildPayload`: if the result still contains `{{`, throw `InvalidOperationException` listing the unresolved token names (regex `\{\{([^}]+)\}\}`) and the AppId. This generalizes the existing userId-only check and catches every mis-mapped field at build time instead of sending broken XML to Eagle.
     * Test: `MapAndPreparePayloadAsync_WithUnmappedPlaceholder_ThrowsListingTokenNames`.
  2. **Corrected `SampleAppConfig.json`** (deliverable — update the copy in `C:\KloudIdentity\Customer Projects\PNB\IDW Eagle\Eagle Soap Request\Working\`; the portal/DB import is done by the config owner):
     * **Create step (ActionName 2):** placeholders → `{{CorrelationId}}` in `eag1:correlationId`; `{{userId}}`, `{{emailAddress}}`, `{{userFullName}}`, `{{companyName}}`. Mappings (plain names, no URN prefixes): `userId ← UserName (IsRequired: true)`, `emailAddress ← ElectronicMailAddresses[0].Value`, `userFullName ← DisplayName (IsRequired: true)`, `companyName ← Constant "Eagle Investment Systems"`. Set `updateSource` literal to `KLOUDIDENTITY`; `businessTaskId` literal `KI_EAGLE_ADD_USER`.
     * **Update step (ActionName 3):** same placeholder/mapping set as Create; replace `{GUID}` with `{{CorrelationId}}`; keep `<processingOptions>REINSERT</processingOptions>` exactly as in the working CHANGE capture; `businessTaskId` → `KI_EAGLE_CHANGE_USER`.
     * **Delete step (ActionName 4):** template placeholder `{{Identifier}}` for `<userId>`; add mapping `DestinationField: "Identifier", SourceValue: "Identifier", MappingType: Direct, IsRequired: true` (satisfies `DeleteAsync`'s hard requirement); `{{CorrelationId}}` for correlation; `updateSource` → `KLOUDIDENTITY`.
     * **Get step (ActionName 1):** endpoint reduced to the bare base URL `https://pnb-d002-star.eagleaccess.com/eagle/v2/users` — the code appends `userid`, `streamName`, `outputFormat` itself (removes the duplicate/unresolved `{{userId}}` query).
     * **AuthenticationFlow:** per Milestone D — `AuthenticationMethodOutbound: 1`, single Basic step (`AuthenticationMethod: 1`) with `BasicAuthentication`-shaped details saved through the portal (Username, KeyVaultReference, EncryptedData.IV).
  3. Add a test that loads the corrected Create template + mappings (fixture copy of the config JSON) and asserts the built payload is structurally equal to the Postman ADD request (normalizing `correlationId`, timestamps) — GAP-TEST-04 seed.
* **Agent Instruction:** "The config JSON is a deliverable artifact, not repo source — do not invent new config schema fields; only correct values within the existing schema. The only production-code change in this milestone is the unresolved-placeholder guard."
* **Checkpoint:** Full suite green; built Create/Update/Delete payloads match the Postman captures structurally; Delete no longer throws the missing-Identifier validation.

### Milestone F: End-to-end verification against PNB dev host
* **Logic:** Prove the cycle with the corrected config and real credentials (config owner executes; not automatable from this repo).
* **Steps (runbook):**
  1. Import corrected AppConfig into the dev tenant (`idw-eagle-dev`).
  2. Create test user (e.g. `KI_PNB_PHASE1_01`) via SCIM → expect Eagle `taskStatusResponse SUCCESS`, `failedRecords 0`.
  3. GET the user via SCIM → expect 200 with `Identifier = KI_PNB_PHASE1_01`.
  4. Update (change email/displayName) → expect SUCCESS; GET reflects changes.
  5. Negative check: send an update forced to fail (e.g. invalid groupName casing per Eagle) → SCIM must surface an error, NOT success.
  6. Delete the user → ack positive; GET returns 404.
  7. Record Eagle `correlationId`s and `eagleStatId`s in the verification log.
* **Checkpoint:** All 6 runbook steps pass; screenshots/response bodies archived under `C:\KloudIdentity\Customer Projects\PNB\IDW Eagle\`.

---

## 🟦 PART 3: TECHNICAL CONSTRAINTS & GUARDRAILS

* **Coding Standards:** .NET 8, file-scoped namespaces, match existing style of `EagleSOAPIntegration.cs`; XML parsing always with `XmlDocument { XmlResolver = null }` (XXE-safe); prefix-agnostic XPath via `local-name()`.
* **Security:** Never log or embed passwords/Authorization headers in exceptions or logs; exception messages for Eagle failures carry trimmed detail (status, severityCode, correlationId, eagleStatId) — not full response bodies with user PII where avoidable.
* **Scope discipline:** Phase 1 only — do NOT implement `accountState` mapping, REINSERT enforcement on `UpdateAsync`, delete-verification GET, dynamic groups, or logging enhancements (those are Phase 2/3). Do not refactor the `ISoapAuthApplier` pipeline.
* **Base-class safety:** `SOAPIntegration.cs` is shared by the generic SOAP integration. Only ONE change is permitted: the fault-detection regex (Milestone C). Milestone D is config + tests only — no changes to `SOAPIntegration`, `AuthContextV2`, or `BasicAuthStrategy`. Every base change requires a full-suite test run.
* **Prohibited:** No new NuGet packages; no manual `new HttpClient()`; no changes to `SOAPParserUtil` placeholder syntax (config aligns to the engine, not vice versa); no `Thread.Sleep`/polling; no swallowing exceptions to "make tests pass".

---

## 🟩 PART 4: VERIFICATION & DEFINITION OF DONE

**Expected Output:**
* `ProvisionAsync` returns `Core2EnterpriseUser { Identifier = <userId from payload> }` only when Eagle reports `SUCCESS` with zero failed records; otherwise throws with actionable Eagle detail.
* `GetAsync` returns the mapped user via REST with `Authorization: Basic …` from the Basic auth-flow step.
* Every SOAP POST carries `Authorization: Basic …` and NO `wsse:Security` element; a configured flow that resolves no token fails before send.
* Built payloads for Create/Update/Delete are structurally identical to the working Postman captures (modulo correlationId/timestamps).

**Unit Test Scenarios:**
* [ ] **Happy Path:** `taskStatusResponse` SUCCESS (ADD + CHANGE golden fixtures) → operation succeeds.
* [ ] **Failure surfaced:** `status=FAILURE` → throws; `failedRecords>0` → throws; message contains status/severityCode/correlationId.
* [ ] **Ack path preserved:** `taskAcknowledgement` isNegative=false passes, isNegative=true throws (existing tests stay green).
* [ ] **Fault detection:** `soapenv:Fault` under HTTP 200 → `HttpRequestException`; generic SOAP tests unaffected.
* [ ] **Auth (SOAP):** Basic flow step → `Authorization: Basic <token>` on the SOAP POST; envelope contains no `wsse:Security`; flow resolving no token → throws before send.
* [ ] **Auth (REST):** Basic flow step → `Authorization: Basic <token>` on GET; no "no token was resolved" exception.
* [ ] **Validation:** payload with any unresolved `{{token}}` → throws listing token names.
* [ ] **Golden request:** corrected Create config produces the Postman ADD request structure.
* [ ] **Regression:** full `dotnet test` suite green.

**Definition of Done:** all boxes above checked + Milestone F runbook executed and archived + PR raised from `fix/125-eagle-integration` referencing issue #125 with the gap IDs closed in the description.

---

## ⬜ PART 5: IMPACT & DEPENDENCIES

* **Impacted Components:**
  * `EagleSOAPIntegration` (primary — response validation, placeholder guard)
  * `SOAPIntegration` base (shared — fault-detection regex only; regression risk covered by full-suite runs)
  * Customer AppConfig `idw-eagle-dev` (templates, mappings, GET endpoint, auth flow switched to Basic)
* **Dependent Tasks:**
  * Phase 2 (accountState mapping, UpdateAsync REINSERT policy, GET round-trip mapping `userId→UserName`, delete verification, correlation-pair logging) — starts only after Milestone F passes.
  * Config owner: re-save the Eagle credential through the portal's Basic-auth flow so `KeyVaultReference` + `EncryptedData.IV` are generated (Milestone D, step 1) — blocker for Milestone F.
  * Milestone F step 2 doubles as live confirmation that the Eagle SOAP endpoint accepts transport Basic (expected per vendor doc; closes GAP-AUTH-03). If it is rejected, STOP and re-open the auth design before proceeding.
* **Anti-Drift Log:** (append during implementation)
  * **Milestone A (14 Jul 2026):** Plan said "the 5 new tests fail"; actual TDD baseline was 4 red + 1 green — `ProvisionAsync_WithTaskStatusSuccess_Succeeds` passes with the defective code by definition (silent success passes everything) and now guards against over-strict validation. Accepted as the correct baseline.
  * **Milestone D (14 Jul 2026):** The no-token tripwire test cannot pass with zero production code — the SOAP path had no guard (only the REST GET path did). Implemented a minimal Eagle-only `GetAuthenticationAsync` override (~15 lines in `EagleSOAPIntegration.cs`) that throws when a configured flow resolves an empty token dictionary. `SOAPIntegration` / `AuthContextV2` / `BasicAuthStrategy` untouched, per the milestone prohibition.
  * **Milestone D step 2 sanity check (read-only):** confirmed `ShouldResolveToken` returns true for `AuthenticationMethodOutbound = Basic`; `ResolveSoapAuthenticationOptions` → null with Basic-shaped details and all three appliers no-op cleanly (envelope untouched — asserted by test); `EagleSoapActionApplier` still sets the SOAPAction header. Basic path works as analyzed.
  * **Milestone E (14 Jul 2026):** Corrected config delivered as a NEW file `SampleAppConfig_Corrected.json` (original `SampleAppConfig.json` preserved as the analysis input). Delete template keeps the capture's hardcoded `effectiveDate` (GAP-DEL-03 is Low / Phase 3 scope; vendor confirms the value is ignored).
  * **Milestone F (14 Jul 2026):** Cannot be executed from the dev workstation (requires portal credential re-save + live provisioning against the PNB host). Delivered as `Phase1_MilestoneF_Verification_Runbook.md` in the customer folder for the config owner to execute.
