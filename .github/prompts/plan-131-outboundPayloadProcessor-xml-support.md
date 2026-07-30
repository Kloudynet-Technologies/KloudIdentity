---
name: Development Task (Plan-First)
about: Technical implementation plan for AI-assisted development
title: "[Dev Task] [Custom Logic] OutboundPayloadProcessor - XML Request/Response Support"
labels: "Dev-Task, Plan-Pending"
assignees: ""
---

<!--
  Context: prerequisite slice for Eagle group-metadata enrichment. The Eagle custom-logic (Logic App)
  call must send/receive XML (the EML payload), but OutboundPayloadProcessor is JSON-only today
  (PostAsJsonAsync + JSON response deserialization). This plan adds an XML path selected by the
  RequestBodyType already present on ExternalEndpointInfo.
  Branch: fix/125-eagle-integration (or successor)
  Confirmed decisions (user, this session):
    1. XML request → XML response (response mirrors the request body type).
    2. XML Content-Type = text/xml.
    3. RequestBodyType.None → throw NotSupportedException.
-->

## 🟥 PART 1: ARCHITECTURAL CONTEXT & INTENT

**Introduction:**
> `OutboundPayloadProcessor.ProcessAsync` is the custom-logic hook that POSTs the outbound provisioning
> payload to an external endpoint (an Azure Logic App) and replaces the payload with the response. Today it is
> **JSON-only**: it calls `PostAsJsonAsync(url, payload as JObject)` and JSON-deserializes the response. The
> Eagle integration produces a **SOAP/EML XML string** payload, so `payload as JObject` yields `null` and the
> whole call is unusable for the group-enrichment scenario.
>
> `ExternalEndpointInfo` already carries a new `RequestBodyType` enum (`None = 0`, `Json = 1`, `Xml = 2`,
> defaulting to `Json`). This task makes `ProcessAsync` honour it: keep the existing JSON behaviour unchanged,
> and add an XML path that sends the payload as `text/xml` and returns the response body as a raw XML string.
> This unblocks the downstream Eagle enrichment work without changing the interface or any caller.

**Endpoint & Inputs:**
* **Method:** `OutboundPayloadProcessor.ProcessAsync(dynamic payload, ExternalEndpointInfo endpointInfo, string correlationID, CancellationToken)`
* **Selector:** `endpointInfo.RequestBodyType` (`Json` default / `Xml` / `None`)
* **JSON payload:** a `JObject` (unchanged)
* **XML payload:** a `string` of XML (new)
* **Auth/headers:** unchanged — `AuthenticationMethod` (APIKey / Bearer / None), `X-Correlation-ID`, 5s timeout

**Architectural Boundaries:**
* **Target Service:** `KN.KloudIdentity.Mapper` (`MapperCore/Outbound/CustomLogic`)
* **Core Patterns:** existing custom-logic processor; `IHttpClientFactory`; Serilog + `IKloudIdentityLogger`
* **Infrastructure:** no new packages; no DB/schema changes; no interface change
* **Files in scope:**
  * `KN.KloudIdentity.Mapper/MapperCore/Outbound/CustomLogic/OutboundPayloadProcessor.cs` (all production changes)
  * `KN.KloudIdentity.MapperTests/Outbound/CustomLogic/OutboundPayloadProcessorTests.cs` (new test file)
  * **No changes** to `IOutboundPayloadProcessor`, `ExternalEndpointInfo` (already has `RequestBodyType`), `ProvisioningBase`, or any integration/orchestrator.

---

## 🟨 PART 2: IMPLEMENTATION PHASES (MILESTONES)
*Execute in order; each gates on `dotnet test` before the next. TDD: write the failing test(s) first in Milestone A.*

### Milestone A: TDD baseline — tests first
* **Logic:** Establish behaviour with tests before refactoring. Use a stub `HttpMessageHandler` to capture the outgoing request (method, URI, `Content-Type`, body) and return canned responses.
* **Steps:**
  1. Create `OutboundPayloadProcessorTests.cs` with an `IHttpClientFactory` mock returning an `HttpClient` over a capturing test handler (mirror the pattern in `EagleSOAPIntegrationTests.TestHttpMessageHandler`).
  2. Tests (initially red where behaviour is new):
     * `ProcessAsync_JsonBodyType_PostsJson_AndDeserializesResponse` — `RequestBodyType.Json`, `JObject` payload → request `Content-Type` is JSON, response JSON deserialized to dynamic. (Locks current behaviour.)
     * `ProcessAsync_DefaultBodyType_BehavesAsJson` — `RequestBodyType` unset (default) → same as Json (backward compat).
     * `ProcessAsync_XmlBodyType_PostsTextXml_AndReturnsRawXmlString` — `RequestBodyType.Xml`, XML string payload → request `Content-Type: text/xml`, exact XML body sent, **response returned as raw string, not deserialized**.
     * `ProcessAsync_XmlBodyType_WithNonStringPayload_Throws` — XML path with a null/non-string payload → clear exception, no HTTP call.
     * `ProcessAsync_NoneBodyType_ThrowsNotSupported` — `RequestBodyType.None` → `NotSupportedException`, no HTTP call.
     * `ProcessAsync_NonSuccessStatus_ThrowsHttpRequestException` — both paths, non-2xx → `HttpRequestException`.
* **Agent Instruction:** "Create only the test file. Confirm the new XML/None tests fail for the documented reason and that the JSON tests pass against the current code."
* **Checkpoint:** Solution builds; JSON tests green; XML + None tests red.

### Milestone B: Refactor `ProcessAsync` into a dispatcher + extract `SendJsonAsync`
* **Logic:** Behaviour-preserving refactor. Move the JSON request/response block into a private method; keep all shared setup in `ProcessAsync`.
* **Steps:**
  1. In `ProcessAsync`, keep (unchanged, in this order): `Validate` → `CreateClient` → `AddAuthenticationHeaders` → add `X-Correlation-ID` → `Timeout = 5s`.
  2. Replace the inline POST/deserialize block with a `switch (endpointInfo.RequestBodyType)`:
     * `Json` → `await SendJsonAsync(httpClient, endpointInfo, payload, correlationID, ct)`
     * (Xml / None added in Milestone C)
  3. Add `private async Task<dynamic> SendJsonAsync(HttpClient client, ExternalEndpointInfo endpointInfo, dynamic payload, string correlationID, CancellationToken ct)` containing the existing logic: `PostAsJsonAsync(endpointInfo.EndpointUrl, payload as JObject, ct)` → `EnsureSuccess` → read string → `JsonConvert.DeserializeObject<dynamic>` → null check → return.
  4. Extract the non-2xx handling into `private void EnsureSuccess(HttpResponseMessage response, string correlationID)` (log + throw `HttpRequestException`) so both paths share it.
  5. Keep the success log + fire-and-forget `CreateLogAsync(endpointInfo, correlationID, ...)` in `ProcessAsync`, after the private method returns (shared across body types).
  6. **Hardening:** in `SendJsonAsync`, if `payload as JObject` is `null`, throw a clear `InvalidOperationException` naming the AppId instead of silently POSTing `null` (today's latent bug).
* **Agent Instruction:** "No behaviour change for the JSON path. The Json + default tests must stay green; XML/None still red."
* **Checkpoint:** JSON + default tests green; full suite green; XML/None still red.

### Milestone C: Implement `SendXmlAsync` + `None` handling
* **Logic:** Add the XML path (request `text/xml`, response returned as raw XML string) and reject `None`.
* **Steps:**
  1. Extend the `switch`: `Xml` → `await SendXmlAsync(...)`; `None` → `throw new NotSupportedException($"RequestBodyType.None is not supported for external custom-logic calls. AppId: {endpointInfo.AppId}")`; add a `default` that also throws `NotSupportedException` (future enum values).
  2. Add `private async Task<dynamic> SendXmlAsync(HttpClient client, ExternalEndpointInfo endpointInfo, dynamic payload, string correlationID, CancellationToken ct)`:
     * `var xml = payload as string;` — if null/whitespace, throw `InvalidOperationException` (XML body type requires a string payload; AppId in message). No HTTP call on failure.
     * `using var content = new StringContent(xml, Encoding.UTF8, "text/xml");`
     * `using var response = await client.PostAsync(endpointInfo.EndpointUrl, content, ct);`
     * `EnsureSuccess(response, correlationID);`
     * `var responseXml = await response.Content.ReadAsStringAsync(ct);`
     * If null/whitespace → throw (empty enrichment response). **Return `responseXml` (the raw XML string) — do NOT JSON-deserialize** (Decision 1: XML request → XML response).
  3. Add `using System.Text;` for `Encoding` if not already present.
* **Agent Instruction:** "The XML path must not touch `JObject`/`JsonConvert`. Return the response body verbatim. `text/xml` is fixed (Decision 2)."
* **Checkpoint:** All Milestone A tests green; full suite green.

### Milestone D: Regression + backward-compatibility verification
* **Logic:** Prove no existing custom-logic behaviour changed and the default holds.
* **Steps:**
  1. Full `dotnet test` — all green.
  2. Confirm (code review) that a config **without** `RequestBodyType` deserializes to the `Json` default (property initializer preserved when the JSON key is absent). If a stored config could carry an explicit `0`, note that it maps to `None` → `NotSupportedException` by design; document for the config owner.
  3. Grep for other `IOutboundPayloadProcessor`/`ProcessAsync` callers to confirm none rely on JSON-specific return typing for a would-be XML endpoint.
* **Checkpoint:** Full suite green; backward-compatibility note recorded in the Anti-Drift Log.

---

## 🟦 PART 3: TECHNICAL CONSTRAINTS & GUARDRAILS

* **Coding standards:** .NET 8; match the file's existing style; dispose `HttpResponseMessage`/`StringContent` with `using`.
* **No interface/contract change:** `IOutboundPayloadProcessor.ProcessAsync` signature and return type (`dynamic`) stay identical. Callers (`ProvisioningBase.ExecuteCustomLogicAsync`) are untouched in this task.
* **Preserve shared behaviour:** auth header logic, `X-Correlation-ID`, the **5-second timeout**, success logging, and `CreateLogAsync` stay in `ProcessAsync` and apply to both paths.
* **XML fidelity:** send the payload string verbatim as `text/xml` (UTF-8); return the response body verbatim. No parsing, no re-serialization, no namespace/entity manipulation in this layer.
* **Security/PII:** do not log full payloads or response bodies (they carry user PII); keep existing identifier-only logging (AppId, correlationID, status).
* **Prohibited:** no new NuGet packages; no change to `RequestBodyType` enum values; no `ResponseBodyType` in this task (response mirrors request per Decision 1); no swallowing of failures.

---

## 🟩 PART 4: VERIFICATION & DEFINITION OF DONE

**Expected Output:**
* `RequestBodyType.Json` (and unset) → identical to today: `PostAsJsonAsync`, JSON response deserialized to `dynamic`.
* `RequestBodyType.Xml` → POST `text/xml` with the exact XML string body; returns the response body as a raw XML `string`.
* `RequestBodyType.None` (and unknown) → `NotSupportedException`, no HTTP call.
* Auth, `X-Correlation-ID`, 5s timeout, and logging apply identically on both paths.

**Unit Test Scenarios:**
* [ ] **JSON happy path:** Json body type → JSON request + deserialized response.
* [ ] **Default = JSON:** unset `RequestBodyType` behaves as Json.
* [ ] **XML happy path:** Xml body type → `Content-Type: text/xml`, exact body, raw-string response (not deserialized).
* [ ] **XML guard:** non-string/null payload on the XML path → throws before any HTTP call.
* [ ] **None:** throws `NotSupportedException`, no HTTP call.
* [ ] **Failure:** non-2xx on both paths → `HttpRequestException` (logged).
* [ ] **Regression:** full `dotnet test` suite green.

**Definition of Done:** all boxes checked; JSON behaviour provably unchanged; XML path returns raw XML; PR notes the confirmed decisions (XML↔XML, `text/xml`, `None`→NotSupported).

---

## ⬜ PART 5: IMPACT & DEPENDENCIES

* **Impacted Components:**
  * `OutboundPayloadProcessor` (sole production file: dispatcher + `SendJsonAsync` + `SendXmlAsync` + `EnsureSuccess`)
  * New test file `OutboundPayloadProcessorTests`
  * `ExternalEndpointInfo.RequestBodyType` — already added (no change here)
* **Dependent Tasks (downstream — not in this task):**
  * Eagle group-metadata enrichment: `EagleSOAPIntegration.MapAndPreparePayloadAsync` (BuildPayload + repeating `<group>` expansion), enrichment at the `ExecuteCustomLogicAsync` point (now XML-capable via this task), and moving reserved-placeholder replace + `EnsureAllPlaceholdersResolved` to `ProvisionAsync`/`ReplaceAsync`/`UpdateAsync`.
  * Config: set `IsExternalAPIEnabled = true`, `ExternalEndpointInfo.EndpointUrl` (Logic App), `RequestBodyType = Xml`, and auth for the Eagle app.
* **Preconditions:** none — this task is self-contained and can merge independently.
* **Anti-Drift Log:** (append during implementation)
  * **Milestone A (14 Jul 2026):** New test file `Outbound/CustomLogic/OutboundPayloadProcessorTests.cs`. Baseline 6 tests: 3 green (JSON/default/failure locks), 3 red (XML happy, XML guard, None) — all red for documented reasons.
  * **Milestone B (14 Jul 2026):** Behaviour-preserving refactor — `ProcessAsync` → dispatcher; extracted `SendJsonAsync`; shared `EnsureSuccess`; success log + `CreateLogAsync` moved to the shared path. Xml/None temporarily routed to JSON (`_ => SendJsonAsync`) to stay behaviour-preserving. Added the JObject null-guard hardening (throws `InvalidOperationException` instead of POSTing null). Full suite 305/308 (same 3 red).
  * **Milestone C (14 Jul 2026):** Added `SendXmlAsync` (`text/xml` request via `StringContent`+`PostAsync`; raw XML string response, no deserialize); switch completed with `Xml` arm and `_ => throw NotSupportedException` (covers `None` + future values). `using System.Text` added. Full suite 308/308.
  * **Milestone D — backward-compat verification (14 Jul 2026):**
    * **Deserialization default confirmed.** `AppConfig` is deserialized via `System.Text.Json.JsonSerializer.Deserialize<AppConfig>` (`AppConfigSnapshotRepository:79,98`) and `JsonConvert.DeserializeObject<AppConfig>` (`GetFullAppConfigQuery:68`). Both leave a property at its C# initializer when the JSON key is absent → an existing config without `RequestBodyType` resolves to `RequestBodyType.Json`. The property is brand-new, so no stored config carries it → all existing custom-logic apps keep JSON behaviour. An explicit stored `0` → `None` → `NotSupportedException` by design.
    * **Caller audit clean.** Only `ProvisioningBase.ExecuteCustomLogicAsync` calls `ProcessAsync`; orchestrators (`CreateUserV4/V2`, `ReplaceUserV4`, `UpdateUserV4`) assign the result back to `payload` and pass it through to the integration — none inspect it as JSON-typed. Returning a raw XML string for XML endpoints flows through unchanged.
    * Release build of `KN.KloudIdentity.Mapper` 0 errors; full suite 308/308.
  * **File-naming note (14 Jul 2026):** This plan was requested as `plan-125-…` but the work is tracked under issue **#131** (a `plan-131-outboundPayloadProcessor-xml-support.md` file plus the `ExternalEndpointInfo.cs` / `OutboundPayloadProcessor.cs` modifications were already present in the working tree at session start). The plan therefore lives at `plan-131-outboundPayloadProcessor-xml-support.md`. Rename to `plan-125-…` if #125 is the intended tracking issue.
