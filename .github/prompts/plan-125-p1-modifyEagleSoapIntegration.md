---
name: Development Task (Plan-First)
about: Technical implementation plan for AI-assisted development
title: "[Dev Task] [SOAP Integration] EagleSOAPIntegration - Remediate Critical Gap Analysis Findings G-01 to G-03"
labels: "Dev-Task, Plan-Pending"
assignees: ""
---

## Plan: EagleSOAPIntegration - Remediate Critical Gap Analysis Findings (G-01 to G-03)

## 🟥 PART 1: ARCHITECTURAL CONTEXT & INTENT

**Introduction:**
The *Eagle Integration — Gap Analysis* (July 2026) reviewed the current `EagleSOAPIntegration` connector on branch `dev-2.0` against Eagle's vendor documentation ("User Management via API/EML", Aug 2022) and live testing against a real PNB Eagle environment (`pnb-d002-star.eagleaccess.com`). This task remediates the following Critical findings:

| ID | Finding | Root Problem |
|----|---------|--------------|
| **G-01** | REST GET parses flat JSON; Eagle's real shape is nested three levels deep | `ParseEagleRestUserResponse` reads top-level `userId`/`name`/`emailAddress`, but the real record lives at `userAdministrationTransactionMessage.userAdministrationTransaction[0].user` with `userFullName` (not `name`). All `TryGetProperty` calls fail silently and the method echoes the caller's identifier back — invisible to monitoring. |
| **G-02** | REST GET omits the documented required `outputFormat`/`streamName` params | `FetchEagleUserViaRestAsync` only appends `?userid=...`. Without `outputFormat=json`, Eagle's response format is not guaranteed and `JsonDocument.Parse` hard-fails on any non-JSON body. |
| **G-03** | `processingOptions=REINSERT` is not implemented anywhere | REINSERT is Eagle's **only** mechanism to revoke or fully replace Center Role access — default CHANGE behavior only merges. Deprovisioning/role-downgrade cannot be satisfied today. |

**Remediation sequencing (mandatory):** G-02 → G-01 → G-03. The URL fix (G-02) guarantees the JSON body the G-01 parser expects; G-03 is mostly independent (template + guard) and goes last.

**Endpoint & Inputs:**

| Operation | Protocol | URI Pattern |
|-----------|----------|-------------|
| Read user (fixed) | REST GET | `%host%/eagle/v2/users?userid={id}&outputFormat=json&streamName=eagle_ml-2-0_default_out_extract_service` |
| Replace user (REINSERT) | SOAP 1.1 | `%host%/EagleMLWebService20` — EML template carries `<processingOptions>REINSERT</processingOptions>` |
| Update user (merge) | SOAP 1.1 | `%host%/EagleMLWebService20` — default CHANGE merge, no REINSERT |

* **Auth:** Unchanged — HTTP Basic Auth via `AuthenticationMethodOutbound = AuthenticationMethods.Basic` (confirmed working in live PNB testing).
* **Real REST response shape (confirmed live against PNB):**

```
userAdministrationTransactionMessage
  └─ userAdministrationTransaction[0]
       └─ user → { userId, userFullName, emailAddress, accountState, ... }
```

**Architectural Boundaries:**
* **Target Service:** `KN.KloudIdentity.Mapper` (MapperCore) — primary file `MapperCore/IntegrationMethods/EagleSOAPIntegration.cs`
* **Core Patterns:** Subclass override pattern on `SOAPIntegration`; existing `ISoapAuthApplier` chain untouched
* **Infrastructure:** No new infrastructure. Golden fixtures in `KN.KloudIdentity.MapperTests`
* **Config dependency:** REPLACE-action EML template (per-tenant DB config) must be authored with the `<processingOptions>REINSERT</processingOptions>` block; PATCH/merge template omits it

---

## 🟨 PART 2: IMPLEMENTATION PHASES (MILESTONES)

*To prevent AI drift, this task must be executed in the following order. Each phase requires a build/test pass before moving to the next.*

### Phase 1: G-02 — REST GET URL Construction

**File:** `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/EagleSOAPIntegration.cs` — `FetchEagleUserViaRestAsync`

* **Logic:**
  1. Add two class constants:
     ```csharp
     private const string DefaultOutputFormat = "json";
     private const string DefaultExtractStreamName = "eagle_ml-2-0_default_out_extract_service";
     ```
  2. Replace the URL construction to append all three parameters. Guard against a base URL that already carries a query string (`baseUrl.Contains('?') ? "&" : "?"` for the first separator) so a tenant-configured URL like `.../users?env=prod` does not produce a double `?`.
     ```csharp
     var separator = baseUrl.Contains('?') ? "&" : "?";
     var url = baseUrl.TrimEnd('/')
         + separator + "userid=" + Uri.EscapeDataString(identifier)
         + "&outputFormat=" + DefaultOutputFormat
         + "&streamName=" + DefaultExtractStreamName;
     ```
  3. `streamName` stays a hardcoded constant for now. Making it per-tenant configurable requires a new `AppConfig`/`ActionStep` field and a migration — deferred unless PNB confirms a non-default stream name (note in PR description, not code comments).
* **Agent Instruction:** "Modify only the URL construction inside `FetchEagleUserViaRestAsync`. Do not touch the auth-header resolution logic in the same method."
* **Checkpoint:** Build passes; GET tests updated to assert the full query string (all three parameters, correctly escaped).

### Phase 2: G-01 — Rewrite `ParseEagleRestUserResponse` for the Real Nested Shape

**File:** `EagleSOAPIntegration.cs` — `ParseEagleRestUserResponse` + new golden fixtures

* **Logic:**
  1. **Capture golden fixtures first.** Check in sanitized live JSON from the PNB environment as `KN.KloudIdentity.MapperTests/SOAPIntegration/EagleInvestment/Fixtures/eagle-rest-get-user.json`, plus a "user not found" variant. Validate exact property casing against the fixture — do not trust memory of it. (Also chips away at gap findings G-10/G-20.)
  2. Rewrite the parser to navigate `userAdministrationTransactionMessage → userAdministrationTransaction[0] → user` and read `userId`, `userFullName` (not `name`), `emailAddress`. Return `null` when the envelope carries no user record.
  3. **Change the not-found contract from silent fallback to explicit signal.** The old method echoed the caller's `identifier` on parse failure — exactly the invisible failure G-01 describes. A missing user must surface as `HttpResponseException(HttpStatusCode.NotFound)` — the only signal the SCIM `ControllerTemplate` maps to HTTP 404 (verified against `ControllerTemplate.Get`; any other exception surfaces as 500). This matches the convention used by the AS400/Linux/SQL/ITSM/REST integrations. An HTTP 404 from Eagle's REST endpoint is treated the same way.
  4. If the top-level payload is not the expected envelope at all (e.g., Eagle returned an error document), throw with the raw body included — do **not** fall through to "not found".
* **Agent Instruction:** "Rewrite only `ParseEagleRestUserResponse` and its call site in `FetchEagleUserViaRestAsync`. Rewrite parser tests to load the golden fixture files instead of the inline `EagleRestUserJson` helper."
* **Checkpoint:** Parser tests pass against real captured fixtures: happy path, empty transaction array, missing `user` node, non-JSON body surfaces a clear error, not-found maps to `HttpResponseException(NotFound)`.

### Phase 3: G-03 — `processingOptions=REINSERT` Support

**Files:** `EagleSOAPIntegration.cs` (`ReplaceAsync` guard); per-tenant EML templates (DB config, documented)

* **Logic:** Part config, part code. PUT-replace vs. PATCH-merge already flow through **separate action templates** (`ReplaceAsync` vs. `UpdateAsync` receive different `ActionStep`s), so the mechanism falls out naturally:
  1. **Template authoring (config):** the REPLACE action's EML template gets a hardcoded `<processingOptions>REINSERT</processingOptions>` block inside the transaction; the PATCH/merge template omits it (Eagle's default CHANGE merge). Document in the Eagle AppConfig authoring notes.
     * **Confirm first** (vendor doc / Eagle team): exact element name, casing, namespace prefix, and placement within `userAdministrationTransaction` — this element has never been sent by this connector, so there is no captured payload to copy from.
  2. **Code guard in `ReplaceAsync`:** before sending, assert the payload contains a `processingOptions` element with value `REINSERT` (XML parse, local-name match — consistent with `ExtractUserIdFromPayload`'s style). Throw a config error if missing, so a mis-authored replace template cannot silently do merge-only semantics. No mirror guard in `UpdateAsync` — merge is the safe default there.
  3. **Decision — no `{{ProcessingOptions}}` runtime token.** A runtime token (alongside the existing `{{CorrelationId}}` replacement) is only useful for a single shared template across both actions; with separate templates per action it is redundant engine surface. Separate templates + the guard is the chosen approach.
* **Agent Instruction:** "Add only the REINSERT payload guard to `ReplaceAsync`. Do not add a new placeholder token to `SOAPParserUtil` or `MapAndPreparePayloadAsync`."
* **Checkpoint:** Replace-path test passes with REINSERT present and throws with it absent (before any SOAP request is sent); update-path test asserts no REINSERT requirement.

### Phase 4: Verification & Live Re-Validation

* **Logic:**
  1. `dotnet build Microsoft.SCIM.sln`
  2. `dotnet test KN.KloudIdentity.MapperTests/KN.KloudIdentity.MapperTests.csproj --filter "FullyQualifiedName~EagleSOAPIntegration" --logger "console;verbosity=minimal"` then the full suite.
  3. **Live re-validation against PNB dev:** run an ADD flow with a fresh test `userId` through the connector and confirm via the REST GET path that the created user parses correctly (correct `Identifier`/`DisplayName`/`UserName` from the real nested shape).
  4. Verify how `Microsoft.SystemForCrossDomainIdentityManagement` translates the not-found signal into SCIM HTTP statuses: `HttpResponseException(NotFound)` → 404 via `ControllerTemplate.Get` (verified — this drove the Phase 2 exception choice).
* **Checkpoint:** Full test suite green; live GET returns a correctly mapped user; missing users surface as SCIM 404.

---

## 🟦 PART 3: TECHNICAL CONSTRAINTS & GUARDRAILS

*Mandatory rules for the AI Agent to follow to ensure long-term maintainability.*

* **Coding Standards:** Match existing file conventions — file-scoped namespaces, collection expressions, `XmlDocument { XmlResolver = null }` for all XML parsing, `System.Text.Json` (no Newtonsoft) for REST parsing.
* **Security:** Keep `XmlResolver = null` (XXE prevention). `Uri.EscapeDataString` on all query parameters. No credentials or full user payloads in logs.
* **Prohibited:**
  * No changes to the generic `SOAPIntegration` base class or `SOAPParserUtil` — all fixes live in `EagleSOAPIntegration` (plus config/fixtures).
  * No new `{{ProcessingOptions}}` placeholder token (see Phase 3 decision).
  * No silent fallbacks: not-found must be an explicit `HttpResponseException(NotFound)` — never echo the caller's identifier back as fake success.
  * No `new HttpClient()` — `IHttpClientFactory` only.
  * No self-authored response shapes in new tests — parser tests must consume the checked-in golden fixture files.

---

## 🟩 PART 4: VERIFICATION & DEFINITION OF DONE

**Expected Output:**
* `GetAsync` returns a `Core2EnterpriseUser` populated from the real nested Eagle shape (`Identifier` = `userId`, `DisplayName` = `userFullName`, `UserName` = `emailAddress`); missing users surface as `HttpResponseException(NotFound)` mapped to SCIM 404.
* Replace operations send `<processingOptions>REINSERT</processingOptions>`; a replace template missing it fails fast with a config error before any request reaches Eagle.

**Unit Test Scenarios:**
* [ ] **G-02 URL:** GET URL contains `userid`, `outputFormat=json`, and `streamName=eagle_ml-2-0_default_out_extract_service`; base URL with an existing query string does not produce a double `?`.
* [ ] **G-01 Happy Path:** Golden fixture (real captured PNB JSON) parses to correct `Identifier`/`DisplayName`/`UserName`.
* [ ] **G-01 Not Found:** Empty `userAdministrationTransaction` array / missing `user` node / HTTP 404 → `HttpResponseException(NotFound)`, never an echoed identifier.
* [ ] **G-01 Malformed:** Non-envelope or non-JSON body → clear error including raw body, not "not found".
* [ ] **G-03 Guard:** Replace payload with REINSERT passes; without it throws config error before any SOAP request is sent; update path has no REINSERT requirement.

---

## ⬜ PART 5: IMPACT & DEPENDENCIES

* **Impacted Components:**
  * `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/EagleSOAPIntegration.cs` (all three fixes)
  * `KN.KloudIdentity.MapperTests/SOAPIntegration/EagleInvestment/` (rewritten tests + new `Fixtures/` golden files)
  * Per-tenant Eagle AppConfig: REPLACE-action EML template must be re-authored with the REINSERT block (config change, coordinated with go-live)
* **Dependent Tasks:**
  * Phase ordering within this task is a hard dependency: G-02 → G-01 → G-03.
  * **Blocked on inputs:** sanitized live PNB REST GET capture (ground truth for Phase 2 fixtures); Eagle team confirmation of exact `processingOptions` element name/casing/placement (Phase 3).
  * Follow-on (out of scope here): G-08 dynamic group templating, G-11 `/user-group` reconciliation reads, G-05 payload-wide placeholder validation.
* **Anti-Drift Log:**
  * 2026-07-13: G-04 (post-write verification) was **descoped from this plan** after its supporting live evidence was retracted as a testing error. The previously implemented verification code (`VerifyEagleWriteAsync`, `EagleWriteUnconfirmedException`, `EagleVerification` settings, related tests) was removed. If Eagle's async ACK semantics later prove to drop writes in practice, reintroduce verification as a separate task.
  * `streamName` is a constant, not per-tenant config, until PNB confirms a non-default stream is needed.
  * `{{ProcessingOptions}}` runtime token explicitly rejected in favor of separate per-action templates + a payload guard.
  * Not-found signal is `HttpResponseException(NotFound)` (not the Mapper's `NotFoundException`) because `ControllerTemplate.Get` handles exceptions itself and only maps that type to 404 — verified during Phase 4.
