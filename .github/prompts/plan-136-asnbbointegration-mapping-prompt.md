---
name: Development Task (Plan-First)
about: Technical implementation plan for AI-assisted development
title: "[Dev Task] [ASNBBoIntegration] Payload Mapping - Roles/Reports/isChecker/isExpire/Branch Resolution"
labels: "Dev-Task, Plan-Pending"
assignees: ""
---



## 🟥 PART 1: ARCHITECTURAL CONTEXT & INTENT

**Introduction:**
The ASNB Back Office (Bo) LOB application (`ASNBBoIntegration`, deriving `RESTIntegrationV4`) needs its
outbound user-provisioning payload enriched with several fields that Entra ID does not — and in some
cases cannot — provide directly:

- `roles` / `reports` — Entra sends both app roles (`ROLE_*`) and report codes as a single mixed
  `appRoleAssignments` list (mapped to `Core2EnterpriseUser.Roles` via the `AppRoleAssignmentsComplex`
  expression). The LOB app needs them split into two separate arrays.
- `isChecker` — derived boolean: true only when the user holds the `ROLE_REFUND_BO` ("Refund
  Backoffice User") role.
- `isExpire` — derived boolean: true when `ExtensionAttribute4` has a value.
- `hqorbranch` / `branchid` — `hqorbranch` comes straight from `ExtensionAttribute2` ("HQ" or
  "BRANCH"). `branchid` is a fixed constant for HQ users, but for branch users it must be resolved by
  calling an external LOB reference-data API (`GET /api/v1/reference/formData`) and matching the
  branch name against `ExtensionAttribute5` (a free-text value like "Cawangan ASNB Segamat").



**Endpoint & Inputs:**
- **Trigger:** Standard outbound user provisioning (POST via `CreateUserV4`, PUT via `ReplaceUserV4`),
  routed to `ASNBBoIntegration` by AppId.
- **Inbound SCIM values:** `Core2EnterpriseUser.Roles` (appRoleAssignments), `KIExtension.ExtensionAttribute2`
  (HQ/BRANCH), `KIExtension.ExtensionAttribute4` (expiry marker), `KIExtension.ExtensionAttribute5`
  (free-text branch name, Entra-side format varies: "Cawangan ASNB X", "Branch ASNB X", etc.).
- **External call:** `GET {host-from-CREATE-action-endpoint}/api/v1/reference/formData` — reference
  data endpoint on the same host as the app's configured CREATE action step; returns
  `data.branches[].{name, code}` among other reference lists.
- **Outbound target fields (top-level in the LOB payload):** `roles` (string[]), `reports` (string[]),
  `isChecker` (bool), `isExpire` (bool), `hqorbranch` (string), `branchid` (string).

**Architectural Boundaries:**
- **Target Service:** ASNB Back Office REST API (`https://testbo.myasnb.com.my/api/v1/...` in test).
- **Core Patterns:** Derived-class override — `ASNBBoIntegration : RESTIntegrationV4`, overrides only
  `MapAndPreparePayloadAsync` (both the 3-arg and the AppConfig-aware 4-arg overload). All auth + CRUD
  logic (`ProvisionAsync`, `ReplaceAsync`, `GetAsync`, `DeleteAsync`) remain inherited, untouched.
- **Routing:** AppId-based dispatch via `IntegrationBaseFactory` + `appsettings.json`. No new
  `IntegrationMethods` enum value.
- **Shared base class change:** `RESTIntegrationV4` gained one new `virtual` method (see Phase 4) —
  every other REST integration's behavior is unchanged.

---

## 🟨 PART 2: IMPLEMENTATION PHASES (MILESTONES)

### Phase 1 : CSV `reports` transform
Original `ASNBBoIntegration` reshaped a comma-separated `ExtensionAttribute1` string into a JSON
`reports` array. Superseded by Phase 2. See [plan-128-ASNBBoIntegration.md](plan-128-ASNBBoIntegration.md).


---

### Phase 2 : Split `roles` / `reports` / `isChecker` from appRoleAssignments
**Logic:**
- Read `resource.Roles` directly (not the schema-mapped payload).
- Partition `Role.Value` entries by a `ROLE_` prefix (case-insensitive): prefixed → `roles`,
  everything else → `reports`.
- `isChecker` = true when `roles` contains `ROLE_REFUND_BO`.
- Force-set `roles` / `reports` / `isChecker` on the payload regardless of what the AppConfig schema
  produced for those fields.

---

### Phase 3: Add `isExpire`
**Logic:**
- `isExpire = !string.IsNullOrWhiteSpace(resource.KIExtension.ExtensionAttribute4)`.
- Set alongside `roles`/`reports`/`isChecker` in the existing 3-arg `MapAndPreparePayloadAsync`
  override (no AppConfig/HTTP call needed for this one).

---

### Phase 4: Enable an AppConfig-aware `MapAndPreparePayloadAsync` overload to actually dispatch
**Problem discovered:** `hqorbranch`/`branchid` resolution needs `AppConfig` (to find the CREATE
action's endpoint and to build an authenticated `HttpClient`). `IIntegrationBase` already declares an
AppConfig-aware overload of `MapAndPreparePayloadAsync` via a **default interface method**, and
`CreateUserV4`/`ReplaceUserV4` both call that overload (for REST integrations) through an
`IIntegrationBaseV2`-typed variable. However, `RESTIntegrationV4` never implements that overload as an
actual class member — it's satisfied purely by the interface's default body, which just forwards to
the 3-arg overload and silently ignores `appConfig`.

The first implementation added a same-signature `public` method directly on `ASNBBoIntegration`
without any base-class hook. It compiled and passed when called on the concrete type, but a dedicated
dispatch test (`MapAndPreparePayload_ThroughInterfaceReference_ResolvesBranchId`, calling through an
`IIntegrationBaseV2` reference — matching exactly how `CreateUserV4`/`ReplaceUserV4` invoke it) failed:
`branchid` came back `null`. A same-signature method in a derived class does not rebind interface
dispatch once an ancestor already satisfies that interface member via its default implementation.

**Fix:** `RESTIntegrationV4` now declares that overload explicitly as a `virtual` class method
(forwarding to the 3-arg version, identical behavior to the previous default for every other
integration):

```csharp
public virtual async Task<dynamic> MapAndPreparePayloadAsync(
    IList<AttributeSchema> schema, Core2EnterpriseUser resource, AppConfig appConfig,
    CancellationToken cancellationToken = default)
{
    return await MapAndPreparePayloadAsync(schema, resource, cancellationToken);
}
```

`ASNBBoIntegration` now properly `override`s this, which dispatches correctly through the interface
reference.


---

### Phase 5: Resolve `hqorbranch` / `branchid`
**Logic:**
- `hqorbranch` = `resource.KIExtension.ExtensionAttribute2` (trimmed), force-set on the payload.
- If `hqorbranch == "HQ"` (case-insensitive) → `branchid = "ASNBJO001"` (fixed constant), **no HTTP
  call made**.
- Otherwise, resolve via `ResolveBranchIdAsync`:
  1. Derive the reference-API host from the app's CREATE action step endpoint
     (`appConfig.Actions.FirstOrDefault(a => a.ActionTarget == USER && a.ActionName == CREATE)`,
     first step by `StepOrder`, scheme+authority only via `new Uri(...).GetLeftPart(UriPartial.Authority)`).
     No CREATE step configured → `InvalidOperationException` (hard failure — this is a config error,
     not a data-quality issue).
  2. `GET {baseUrl}/api/v1/reference/formData` using the inherited `CreateHttpClientAsync` (same auth
     as the main Bo endpoint). Non-2xx response → `HttpRequestException` (hard failure).
  3. Extract the branch-matching keyword from `ExtensionAttribute5` and match it against
     `data.branches[].name` (case-insensitive `Contains`) to get `data.branches[].code`.


---

### Phase 6: Change "no match" from a hard failure to `branchid = ""`
**Change:** Originally, a missing branch keyword or no branch match threw `InvalidOperationException`.
Per updated requirements, both cases now log a warning and set `branchid = ""` instead — only
genuine configuration/connectivity problems (no CREATE step, reference API call failure) still throw.

---

## 🟦 PART 3: TECHNICAL CONSTRAINTS & GUARDRAILS

- **Override scope:** `ASNBBoIntegration` overrides only the two `MapAndPreparePayloadAsync`
  overloads. `ProvisionAsync`, `ReplaceAsync`, `GetAsync`, `UpdateAsync`, `DeleteAsync` remain fully
  inherited from `RESTIntegrationV4` — the AppConfig-aware overload (Phase 4) is what makes
  `AppConfig`/HTTP access available without touching those methods.
- **Read from the resource, not the schema:** `roles`/`reports`/`isChecker`/`isExpire`/`hqorbranch`
  are all read directly off `Core2EnterpriseUser` (`resource.Roles`, `resource.KIExtension.*`) and
  force-set on the payload, independent of whatever the AppConfig's AttributeSchema does or doesn't
  map for those field names.
- **Fail hard only on configuration/connectivity errors:** missing CREATE action step, or the
  reference API returning a non-2xx response, both throw. A user's branch name simply not matching
  anything (or the marker being absent) is a data-quality condition, not a system fault — it degrades
  to `branchid = ""` with a logged warning instead of blocking provisioning.
- **No new `IntegrationMethods` enum value; routing stays AppId-based.**
- **`JObject` normalization:** payload is always normalized via
  `payload as JObject ?? JObject.FromObject(payload)` before mutation, never mutated as `dynamic`.
- **Base-class change is additive only:** the new `RESTIntegrationV4` virtual overload's default body
  is behaviorally identical to the interface default it replaces — every other REST integration
  (`ASNBKioskIntegration`, etc.) is unaffected unless it explicitly overrides it.
- **Constants:** all ASNB Bo Integration field names/markers live in `AppConstant` under their own
  region, not scattered as local `private const`s.

---

## 🟩 PART 4: VERIFICATION & DEFINITION OF DONE

**Expected Output (final payload shape):**
```json
{
  "roles": ["ROLE_PORTALADMIN_BO"],
  "reports": ["PAC01A", "PAC01R"],
  "isChecker": false,
  "isExpire": false,
  "hqorbranch": "BRANCH",
  "branchid": "ASNBJO001"
}
```

**Test Coverage (`KN.KloudIdentity.MapperTests/MapperCore/PNB/ASNBBoIntegrationTests.cs`):**
- [x] Roles/reports split by `ROLE_` prefix.
- [x] `isChecker` true/false (Refund Backoffice role present/absent).
- [x] No roles at all → empty arrays, `isChecker` false.
- [x] `isExpire` true (ExtensionAttribute4 set) / false (empty).
- [x] HQ user → fixed `branchid`, **no HTTP call made** (asserted via a call-tracking flag).
- [x] Branch user → `branchid` resolved from the mocked reference API response.
- [x] No branch match → `branchid = ""` (not a throw).
- [x] No CREATE action step configured → `InvalidOperationException` (still a hard failure).
- [x] Missing branch keyword → `branchid = ""` (not a throw).
- [x] `[Theory]`: `"ASNB"` marker matched regardless of what precedes it (`Cawangan`, `Branch`,
      arbitrary text).
- [x] No `"ASNB"` marker present at all → `branchid = ""`.
- [x] Dispatch sanity check through an `IIntegrationBaseV2` reference (the bug caught in Phase 4).

**Definition of Done:**
- [x] `ASNBBoIntegration` builds with 0 errors.
- [x] `ASNBBoIntegrationTests`: 16/16 passing.
- [x] Full solution test suite: 325/325 passing (no regressions from the `RESTIntegrationV4` change).
- [ ] Changes committed (currently uncommitted working-tree changes on top of `470308d` — pending
      user confirmation before commit, per this session's "only commit when explicitly asked" rule).
- [ ] MgtPortal AppConfig verified in a live/test environment (CREATE action step endpoint present,
      auth valid against the actual `testbo.myasnb.com.my` reference API) — not exercised by unit
      tests, which mock the HTTP layer.

---

## ⬜ PART 5: IMPACT & DEPENDENCIES

**Impacted Components:**
- `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/PNB/ASNBBoIntegration.cs` — roles/reports/
  isChecker/isExpire/hqorbranch/branchid mapping logic.
- `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/RESTIntegrationV4.cs` — new `virtual`
  AppConfig-aware `MapAndPreparePayloadAsync` overload (shared by every REST integration; behavior
  change only for classes that explicitly override it, currently just `ASNBBoIntegration`).
- `KN.KloudIdentity.Mapper/Utils/AppConstant.cs` — new `ASNB Bo Integration` region.
- `KN.KloudIdentity.MapperTests/MapperCore/PNB/ASNBBoIntegrationTests.cs` — rewritten/extended.

**No changes to:**
- `IntegrationMethods` enum, `IntegrationBaseFactory`, DI registration, `appsettings.json` routing
  (all already in place from the original `ASNBBoIntegration` setup).
- `Microsoft.SystemForCrossDomainIdentityManagement` (SCIM protocol library).
- Any Group provisioning or inbound sync code.

**Dependent Tasks:**
- MgtPortal AppConfig for the ASNB Bo app must have a CREATE action step pointing at the correct host
  (used to derive the reference-data API base URL) — required for the branch-resolution path to work
  at all.
- Entra ID attribute mappings must populate `ExtensionAttribute2` ("HQ"/"BRANCH"), `ExtensionAttribute4`
  (expiry marker), and `ExtensionAttribute5` (branch free-text) as described in Part 1.

**Anti-Drift Log:**
- **Interface dispatch bug (Phase 4):** the first attempt at the AppConfig-aware overload was a
  same-signature method with no relationship to `RESTIntegrationV4`'s class hierarchy. It compiled and
  passed a naive concrete-type test, but silently never ran through the actual `IIntegrationBaseV2`
  call path used in production (`CreateUserV4`/`ReplaceUserV4`) — `branchid`/`hqorbranch` would have
  shipped as always-missing. Caught only because a dispatch-specific test was added deliberately
  mirroring the real call path, not because of a type error. Fixed by giving `RESTIntegrationV4` a real
  `virtual` class member for that overload.
- **"Cawangan " prefix stripping was too narrow:** the first branch-matching implementation stripped a
  literal `"Cawangan "` prefix. Superseded by the more general "match from the ASNB marker word onward"
  rule (Phase 7) once it became clear the prefix isn't fixed.
- **Hard-fail vs. soft-fail on no branch match:** initially every unresolved branch match threw
  `InvalidOperationException`. Changed to `branchid = ""` (Phase 6) per updated requirements — this was
  a deliberate behavior reversal, not a bug fix.
