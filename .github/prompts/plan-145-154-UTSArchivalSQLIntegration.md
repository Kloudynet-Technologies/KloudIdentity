---
name: Development Task (Plan-First)
about: Technical implementation plan for AI-assisted development
title: "[Dev Task] [PNB] - SCIM Connector Service - Implement UTSArchivalSQLIntegration (UTS Archival Stored Procedure Provisioning)"
labels: "devtask, PNB"
assignees: "arifulkloudynet"
---

## 🔗 GITHUB TRACKING

**Parent issue:** [#145](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/145) (labels `devtask`, `PNB`; assignee `arifulkloudynet`). Each phase is a sub-issue of #145.

| Phase | Issue | Scope | Status |
|---|---|---|---|
| Phase 0 | [#146](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/146) | Base `SQLIntegration` → `IIntegrationBaseV2` (generic prerequisite) | Implemented (uncommitted) |
| Phase 1 | [#147](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/147) | `UTSArchivalSQLIntegration` skeleton (reuses base engine) | Implemented (uncommitted) |
| Phase 2 | [#148](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/148) | Core executor — named call + response-code capture (**in base `SQLIntegration`**) | Implemented (uncommitted) |
| Phase 3 | [#149](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/149) | Create (`ProvisionAsync`) + parameter mapping | Implemented (uncommitted) |
| Phase 4 | [#150](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/150) | Get (`GetAsync`) | Implemented (uncommitted) |
| Phase 5 | [#151](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/151) | Update (`UpdateAsync`) — PATCH semantics and status | Implemented (uncommitted) |
| Phase 6 | [#152](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/152) | Delete (`DeleteAsync`) | Implemented (uncommitted) |
| Phase 7 | [#153](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/153) | DI registration + AppId routing | Open |
| Phase 8 | [#154](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/154) | Tests + local end-to-end verification | Open |

---

## 🟥 PART 1: ARCHITECTURAL CONTEXT & INTENT

**Introduction:**
UTS Archival (PNB, database `LoginArchival`) is provisioned through four stored procedures delivered by the application team:

| Operation | Stored procedure | Lookup key |
|---|---|---|
| Create (POST) | `dbo.usp_CreateUTSArchivalUser` | – |
| Get (GET) | `dbo.usp_GetUTSArchivalUser` | `@LoginID` |
| Update (PATCH) | `dbo.usp_UpdateUTSArchivalUser` | `@LoginID` |
| Delete / Disable | `dbo.usp_DeleteUTSArchivalUser` | `@LoginID` (disable only, Flag = 2) |

These procedures never raise errors. Every outcome is reported via OUTPUT parameters `@ResponseCode INT` / `@ResponseMessage NVARCHAR(500)` (0 Success, 1 Validation Error, 2 Not Found, 3 Conflict, 4 System Error). The Create/Update/Delete SPs also return a single user row on success.

The generic `SQLIntegration` cannot drive this contract:

| # | Generic `SQLIntegration` behaviour (dev-2.0) | Effect on UTS Archival |
|---|---|---|
| 1 | Success = `ExecuteNonQuery` did not throw; OUTPUT params never declared/read | Every failure (duplicate, invalid role, not found, system error) is reported to Entra as success |
| 2 | Create returns `null` → SCIM id stays the random GUID set in `NonSCIMUserProvider.cs:80` | GET/PATCH/DELETE send a GUID; the SPs look up by `@LoginID` → always "not found" |
| 3 | `{CALL sp (?,?,…)}` bound **by position** | Mapping order must exactly mirror SP parameter order; OUTPUT params appear as mappable inputs |
| 4 | PATCH builds a fresh `Core2EnterpriseUser` (`UpdateUserV4.cs:46-48`); unpatched value types become `0`/`false` | `@LevelsID = 0` fails validation; `active` is `false` on every PATCH that did not touch it |
| 5 | `GetAsync` never calls `reader.Read()` (`SQLIntegration.cs:325-368`) | GET throws on column access |
| 6 | `SQLIntegration` implements only `IIntegrationBase` and is registered as such (`ServiceExtension.cs:86`), but `IntegrationBaseFactory` resolves only `IIntegrationBaseV2` | SQL integration is not resolvable at all on dev-2.0 |

Following the PNB precedent (`ASNBKioskIntegration`, `ASNBBoIntegration` derive from `RESTIntegrationV4` and are routed by AppId), a dedicated `UTSArchivalSQLIntegration : SQLIntegration` implements the UTS contract, while generic defects (#5, #6) are fixed once in the base class for all SQL customers.

**Endpoint & Inputs:**
- **Inbound:** SCIM `POST/GET/PATCH/DELETE /Users` from Entra ID provisioning (via `NonSCIMUserProvider` → `CreateUserV4` / `GetUserV4` / `UpdateUserV4` / `DeleteUserV4`)
- **Outbound:** ODBC → SQL Server `10.40.11.6`, database `LoginArchival`, driver `ODBC Driver 17 for SQL Server`
- **Auth:** SQL authentication (DSN fields + password stored AES-encrypted in MgtPortal `SqlAuthenticationMethods`); IGA service account to be issued by PNB via SR
- **Environment:** Production only — PNB has **no test environment**. All development testing runs against the local reconstruction (see Part 4).

**Architectural Boundaries:**
- **Target Service:** `Microsoft.SCIM.WebHostSample` (SCIM service in AKS, namespace `kloudidentity-prod`) — executes ODBC directly; LocalAgent and RabbitMQ are NOT involved
- **Core Patterns:** Derived class override (inherits `SQLIntegration`), AppId routing via `IntegrationMappings.AppIdToIntegration`
- **Infrastructure:** `System.Data.Odbc`, SQL Server stored procedures; no schema changes in `LoginArchival`

**MgtPortal App Configuration (target state):**

| Field | Value |
|---|---|
| Integration Method (Outbound) | `SQL` |
| Authentication | Upload `LoginArchival_Prod.dsn` (Driver / Server / Database / UID) + set password |
| PostSpName | `dbo.usp_CreateUTSArchivalUser` |
| GetSpName | `dbo.usp_GetUTSArchivalUser` |
| PatchSpName | `dbo.usp_UpdateUTSArchivalUser` |
| DeleteSpName | `dbo.usp_DeleteUTSArchivalUser` |

User attribute mappings (`DestinationField` = SP parameter name; order is irrelevant after this change):

| HttpRequestType | SCIM source (`SourceValue`) | DestinationField | Type | Notes |
|---|---|---|---|---|
| POST | `UserName` | `@LoginID` | String(50) | Entra `userName` (set via the Entra provisioning attribute mapping) → SCIM `UserName` → `@LoginID`. Must be stable (LoginID is immutable) |
| POST, PATCH | `DisplayName` | `@Name` | String(100) | |
| POST, PATCH | `Roles[0]:Value` *(or Constant `1`)* | `@LevelsID` | Integer | 1 = Normal User, 3 = System Admin; single-valued |
| POST | `EnterpriseExtension:EmployeeNumber` | `@ReferenceNo` | String(20) | Staff number; `ReferenceID` fixed to 7 inside the SP |
| POST, PATCH | `ElectronicMailAddresses[0]:Value` | `@Email` | String(100) | |
| POST | Constant `0` | `@CreatedBy` | Long | Optional |
| PATCH | Constant `0` | `@UpdatedBy` | Long | Optional |
| POST, PATCH | **Constant** `Output:ResponseCode` | `@ResponseCode` | Integer | SP OUTPUT — status code (required marker) |
| POST, PATCH | **Constant** `Output:ResponseMessage` | `@ResponseMessage` | String | SP OUTPUT — status message |

**Output-parameter marker:** an attribute row whose `SourceValue` is `Output` or `Output:<Role>` (case-insensitive) declares an SP **OUTPUT** parameter. `MappingType = Constant` is the convention (it is not an Entra/SCIM attribute); `Direct` with the same value is accepted too. The marker is evaluated **before** `MappingType`, so the literal is never bound as an input value. Roles: `ResponseCode` (status code, interpreted), `ResponseMessage` (status text, logged/returned), plain `Output` (captured, not interpreted). Nothing is added to the Entra provisioning schema.

> **MgtPortal schema detection creates one attribute row per SP parameter** (POST SP → 17 rows, PATCH SP → 18 rows, including `@ResponseCode` / `@ResponseMessage` because the Metaverse query does not filter `is_output`), each with `SourceValue = ""`, `IsRequired = true`, `DefaultValue = "N/A"` (`AddOrEditSqlUserAttributesCommandHandler.cs:46-60`). Re-running detection deletes and recreates all rows (existing mappings are preserved). The admin only fills `SourceValue` for the rows in the table above (including the two output markers on both the POST and PATCH rows) and leaves the rest empty — the connector must tolerate the unmapped rows (Phase 3). Detection does not cover the Get / Delete SPs, so they reuse the **POST** output markers (all four UTS SPs share the same OUTPUT parameter names).
>
> `@LoginID` for PATCH/DELETE/GET is **not** mapped — it is taken from the SCIM identifier, which equals LoginID after Phase 3. `@Flag` is never mapped — it is derived from `active` (Phase 5). `SourceValue` paths confirmed against `JSONParserUtilV2.ReadProperty` / `Core2EnterpriseUser` in Phase 3 (`UserName`, `DisplayName`, `Roles[0]:Value`, `EnterpriseExtension:EmployeeNumber`, `ElectronicMailAddresses[0]:Value`).

---

## 🟨 PART 2: IMPLEMENTATION PHASES (MILESTONES)

### Phase 0: Base `SQLIntegration` → `IIntegrationBaseV2` (generic prerequisite) — [#146](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/146)

**Logic:**
- Change `public class SQLIntegration : IIntegrationBase` → `: IIntegrationBaseV2`.
- Implement the five V2 overloads (`ProvisionAsync`, `GetAsync`, `ReplaceAsync`, `UpdateAsync`, `DeleteAsync` with `appId` / `ActionStep`) by delegating to the existing V1 methods (`actionStep` is unused for SQL). Keep `ReplaceAsync` as `NotSupportedException`.
- Mark the public V1 CRUD methods `virtual` so the derived class can override them.
- Extract `protected async Task<OdbcConnection> OpenConnectionAsync(AppConfig, CancellationToken)` (wraps `GetAuthenticationAsync` + `OpenAsync`) and use `await using` so connections are disposed on exceptions (today they leak when `ExecuteNonQueryAsync` throws).
- Fix `GetAsync`: call `await reader.ReadAsync()` before reading columns; dispose the reader.
- `ServiceExtension.cs:86`: `services.AddScoped<IIntegrationBase, SQLIntegration>()` → `services.AddScoped<IIntegrationBaseV2, SQLIntegration>()`.
- **Output-marker support (generic):** add `SqlOutputMapping` record (`ParameterName`, `Role`, `OdbcType`/SQL type) and `protected static bool IsOutputMapping(AttributeSchema)` / `protected static IReadOnlyList<SqlOutputMapping> GetOutputMappings(IEnumerable<AttributeSchema>)` to `SQLIntegration` (constants `OutputMarker = "Output"`, roles `ResponseCode`, `ResponseMessage`). Base `MapAndPreparePayloadAsync` skips output-marker rows (never binds the marker literal). No other change to base CRUD behaviour; the named-batch engine is added to the base in Phase 2 (opt-in for derived classes).
- SQL type for a declared output variable comes from the row's `DestinationType` (`AttributeDataTypes`): String/NVarChar/VarChar/Char/NChar/Text/NText → `NVARCHAR(4000)`, `Int` → `INT`, `Number`/`BigInt` → `BIGINT`, `SmallInt` → `SMALLINT`, `TinyInt` → `TINYINT`, `Boolean`/`Bit` → `BIT`, `DateTime`/`SmallDateTime` → `DATETIME2`, `Date` → `DATE`, `Time` → `TIME`, `Decimal`/`Numeric` → `DECIMAL(18,2)`, binary types → `NotSupportedException`. Detection's `max_length` (bytes) is not used for outputs.
- `GetAuthenticationAsync` with `AppConfig.AuthenticationDetails` from the snapshot repository (`System.Text.Json` → `JsonElement`): **confirmed broken** — even `config.AuthenticationDetails == null` throws `RuntimeBinderException` on a `JsonElement` (same for `appConfig.IntegrationDetails == null` in `GetAsync`). Fix: treat the value as `object`, check null / JSON-null via `IsMissing`, deserialize via `ToString()` into `SQLAuthentication`, then validate the typed object; non-JSON → `ArgumentException`.
- **Implemented (dev-2.0, uncommitted):** `SqlOutputMapping` lives in `KN.KloudIdentity.Mapper.Domain/SQL/`; `OpenConnectionAsync(AppConfig, string correlationId, CancellationToken)` takes the correlation id for logging; added `protected static GetIntegrationDetails(AppConfig)`, `GetParameterName(AttributeSchema)`, `ToSqlDeclarationType(AttributeDataTypes)`; `CreateOdbcParameter` is `protected static`; V2 overloads and both `MapAndPreparePayloadAsync` overloads are `virtual`. Tests: `SqlIntegration/SQLIntegrationTest.V2.cs`, `SQLIntegrationTest.OutputMapping.cs` (58 SQL tests / 369 total green). Not unit-testable without a DB: `GetAsync` `ReadAsync` fix and connection disposal — covered by Phase 8 local E2E.

**Agent Instruction:** "Make only the generic changes listed. Do not add any UTS-specific logic. Do not change the positional `{CALL …}` behaviour of the base class."

**Checkpoint:** Solution builds; existing `KN.KloudIdentity.MapperTests/SqlIntegration/*` tests pass (update only where they assert the old interface); `IntegrationBaseFactory.GetIntegration(IntegrationMethods.SQL)` resolves `SQLIntegration`.

---

### Phase 1: `UTSArchivalSQLIntegration` skeleton + response model — [#147](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/147)

> **Design decision (2026-09-25):** `SQLIntegration` is the **generic SQL engine** for all SQL customers. The reusable mechanics (named-parameter batch, output markers, result-set reading, response-code check) live in the base as `protected` members; `UTSArchivalSQLIntegration` is thin and holds only UTS rules. Base CRUD keeps the positional `{CALL}` behaviour — the engine is opt-in for derived integrations, so existing SQL apps see no change.

**Logic:**
- Create `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/PNB/UTSArchivalSQLIntegration.cs`, class `UTSArchivalSQLIntegration : SQLIntegration`, same constructor dependencies as the base; `IntegrationMethod = IntegrationMethods.SQL` (no new enum value).
- **No UTS response model** — reuse the generic `SqlProcedureResult` / `SqlProcedureResponseCode` from `KN.KloudIdentity.Mapper.Domain/SQL/` (Phase 2).
- **No hardcoded parameter lists.** Parameter names come from the attribute mapping (`DestinationField`); outputs from the marker rows via base `GetOutputMappings(AppConfig, HttpRequestTypes)` (Get / Delete fall back to POST rows).
- `private static IReadOnlyList<SqlOutputMapping> ResolveStatusOutputs(AppConfig, HttpRequestTypes)`: base `GetOutputMappings(appConfig, requestType)` + **UTS rule**: no `Output:ResponseCode` found → `InvalidOperationException("No 'Output:ResponseCode' mapping configured for AppId …")` (config error; never fall back to hardcoded names).
- Lookup parameter for Get / Update / Delete = the `DestinationField` of the **POST** attribute whose `SourceValue` is `UserName` (e.g. `@LoginID`). Resolve via `private static string ResolveLookupParameter(AppConfig)`; throw `InvalidOperationException` if not mapped. (SCIM Identifier = LoginID = UserName after Phase 3, so the same parameter is used for all lookups.)

**Agent Instruction:** "Create only the class skeleton, `ResolveLookupParameter` and `ResolveStatusOutputs`. Reuse the base engine; do not duplicate it. No CRUD method bodies."

**Checkpoint:** Builds; class is resolvable as `IIntegrationBaseV2`.

✅ Implemented: `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/PNB/UTSArchivalSQLIntegration.cs` (namespace `KN.KloudIdentity.Mapper.MapperCore`, like the other PNB classes). `ResolveLookupParameter` / `ResolveStatusOutputs` are `protected static` (tested via a subclass; the Mapper project has no `InternalsVisibleTo`). Tests: `KN.KloudIdentity.MapperTests/MapperCore/PNB/UTSArchivalSQLIntegrationTests.cs` (existing PNB test folder) — lookup resolution, POST fallback for GET/DELETE, renamed `@RetCode`, missing `Output:ResponseCode`, factory routing by AppId. Not yet registered in DI (Phase 7).

---

### Phase 2: Core executor — named call + response-code capture — [#148](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/148)

> **Implemented in the base `SQLIntegration`** (generic, SQL Server only; uncommitted on dev-2.0). The UTS class calls these; it does not implement its own executor.

**Members (all `protected`):**
- `Task<SqlProcedureResult> ExecuteProcedureAsync(AppConfig, string procedureName, IReadOnlyList<OdbcParameter> inputs, IReadOnlyList<SqlOutputMapping> outputs, string correlationId, CancellationToken)` — builds the batch, opens the connection (`OpenConnectionAsync`), requires a SQL Server ODBC driver (T-SQL batch; `NotSupportedException` otherwise — driver check also accepts versioned Linux names `LIBMSODBCSQL*`), binds inputs, reads results.
- `static string BuildProcedureBatch(procedureName, inputs, outputs)` — pure, unit-tested:
  ```sql
  SET NOCOUNT ON;
  DECLARE @o0 INT, @o1 NVARCHAR(4000);
  EXEC dbo.usp_CreateUTSArchivalUser @LoginID = ?, @Name = ?, …,
       @ResponseCode = @o0 OUTPUT, @ResponseMessage = @o1 OUTPUT;
  SELECT @o0 AS [o0], @o1 AS [o1];
  ```
  Output columns are aliased `[o0]`, `[o1]`, … (generated by code, matched to outputs by position) — never taken from config text. No outputs → no `DECLARE` / `SELECT`.
- `static Task<SqlProcedureResult> ReadProcedureResultAsync(DbDataReader, outputs, CancellationToken)` — walks all result sets; with outputs, the **last** is the output row and the one before it (if any) is the procedure row; without outputs, the last result set is the row. Row = first row as `column → value` (`DBNull` → `null`, case-insensitive). Output row missing, or mapped `ResponseCode` NULL → `InvalidOperationException`. Plain `Output` values are returned in `Outputs` by parameter name.
- `virtual void ThrowIfFailed(SqlProcedureResult, procedureName, AppConfig, correlationId)` — no-op when `ResponseCode` is null (no status contract) or 0; otherwise logs code / message / SP / AppId / CorrelationId (no parameter values) and throws `HttpResponseException(MapResponseCode(code))`.
- `virtual HttpStatusCode MapResponseCode(int)` — default convention (`SqlProcedureResponseCode`):

  | ResponseCode | HTTP | Notes |
  |---|---|---|
  | 0 | – | success |
  | 1 | 400 BadRequest | |
  | 2 | 404 NotFound | |
  | 3 | 409 Conflict | UTS Delete overrides this (Phase 6) |
  | 4 / unknown | 500 InternalServerError | |

- `static IReadOnlyList<SqlOutputMapping> GetOutputMappings(AppConfig, HttpRequestTypes)` — marker rows of the operation, falling back to POST rows.

**Rules (unchanged):**
- Binding by name removes the mapping-order dependency and avoids ODBC OUTPUT-parameter quirks. Pattern verified with `System.Data.Odbc` + ODBC Driver 17 against the local `LoginArchival`: success → `[user row] + [status row]`; validation/conflict → `[status row]` only.
- **NULL / omission** (applied by the caller's mapping, Phase 3): every *mapped* parameter is always emitted (missing value → `DBNull`, so required SP params give code 1 → 400, not SQL error 201 → 500); *unmapped* parameters are not emitted (SP default applies).
- **Injection guard:** input and output names must match `^@[A-Za-z0-9_]{1,127}$`, the SP name `^[A-Za-z0-9_\.\[\]]{1,256}$`; duplicates (case-insensitive, incl. input = output) rejected → `ArgumentException`. Values are always `OdbcParameter`s.
- A well-formed name that is not an SP parameter (typo) is rejected by SQL Server (verified: error 8144 "has too many arguments specified", surfaced as `OdbcException`) → 500 with the SQL message logged.

**Checkpoint:** ✅ `SqlIntegration/SQLIntegrationTest.ProcedureExecution.cs` — batch text, name validation, result-set parsing (via `DataTableReader`), response-code mapping, POST fallback. 84 SQL tests / 395 total green. `ExecuteProcedureAsync` itself verified live (2026-09-25) against the local Docker `LoginArchival` (ODBC Driver 17, auth details passed as `JsonElement` like the snapshot) via a scratch harness — 10/10:

  | Call | ResponseCode | HTTP | Row |
  |---|---|---|---|
  | Create new user | 0 | – | yes (Flag 99) |
  | Create duplicate | 3 | 409 | none |
  | Create with inactive role (LevelsID 2) | 1 | 400 | none |
  | Create with mapped `@Name` = NULL | 1 | 400 (not SQL 201) | none |
  | Get existing | 0 | – | yes |
  | Get unknown | 2 | 404 | none |
  | Delete | 0 | – | yes (Flag 2) |
  | Delete again | 3 | 409 (UTS overrides → success, Phase 6) | none |
  | Mapping typo `@Nmae` | – | `OdbcException` (SQL 8144) | – |
  | Injection in parameter name | – | `ArgumentException` before any SQL | – |

---

### Phase 3: Create (`ProvisionAsync`) + parameter mapping — [#149](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/149)

**Logic:**
- Override `MapAndPreparePayloadAsync(IList<AttributeSchema>, Core2EnterpriseUser, AppConfig, …)` to return `List<OdbcParameter>` keyed by name (reuse base value resolution: `Direct` via `JSONParserUtilV2`, `Constant` via `SourceValue`).
- **Tolerate MgtPortal-detected rows** (do NOT call the base `ValidateAttributeSchema`, which throws on empty `SourceValue`):
  - `SourceValue` null/empty → **skip** (parameter not emitted → SP default applies).
  - Output-marker rows (`SourceValue` = `Output` / `Output:<Role>`, any `MappingType`) → **never an input**; they are passed to the executor as outputs.
  - `DefaultValue == "N/A"` → treat as no default (never bind the literal `'N/A'`); ignore detection's `IsRequired = true` — the SP validates required fields (code 1 → 400).
  - Use rows whose `HttpRequestType` matches the operation (POST → Create, PATCH → Update).
  - `DestinationTypeLength` from detection is `sys.parameters.max_length` (bytes; doubled for nvarchar) — use as-is, the SP parameter size still governs.
  - If no mapped row remains for an operation → throw `InvalidOperationException` (config error).
- Value normalization for UTS:
  - Strings: trim; empty → `DBNull`.
  - `@LevelsID`: parse to `int`; unparsable or `0` → `DBNull` (lets the SP return code 1 on Create).
  - `DateTime.MinValue` → `DBNull` (SQL `DATETIME` cannot hold `0001-01-01`).
- Override both `ProvisionAsync` overloads: call `ExecuteProcedureAsync(PostSpName, …)`, `ThrowIfFailed`, then return
  `new Core2EnterpriseUser { Identifier = <LoginID>, UserName = <LoginID> }` using the `LoginID` column of the returned user row (fallback: the value sent for the lookup parameter).
- Result: `CreateUserV4.cs:188-190` copies `result.Identifier` into the SCIM resource → Entra stores **LoginID** as the SCIM `id`, and all later GET/PATCH/DELETE calls receive LoginID.

✅ **Implemented (2026-09-25):**
- Generic part in base `SQLIntegration`: `protected List<OdbcParameter> MapProcedureInputs(schema, resource)` + `protected virtual object ConvertInputValue(AttributeSchema, object?)` (skip empty `SourceValue` / markers, emit mapped-but-missing as `DBNull`, apply `DefaultValue` except `"N/A"`, trim strings, typed conversion, `DateTime.MinValue` → `DBNull`, unconvertible Direct → `DBNull` + warning, invalid Constant → `ArgumentException`). Values are read with `JSONParserUtilV2.ReadProperty`, **not** `GetValue`: `GetValue` returns `DefaultValue` (`"N/A"`, which throws `FormatException` for Int/DateTime) for `IsRequired` rows and depends on the process-wide `_isSamplePayload` flag. `ExecuteProcedureAsync` is now `protected virtual` (unit tests fake it).
- UTS: overrides both `MapAndPreparePayloadAsync` overloads (no rows / no mapped rows → `InvalidOperationException`), `ConvertInputValue` (Direct integer `0` → `DBNull`; Constant `0` kept, e.g. `@CreatedBy`), and V1 `ProvisionAsync` (V2 delegates to it). Config (PostSpName, `Output:ResponseCode`, lookup parameter) is resolved **before** the DB call. Identifier = lookup column of the returned row (`@LoginID` → `LoginID`), fallback to the sent value.
- Tests: `MapperCore/PNB/UTSArchivalSQLIntegrationTests.Create.cs` (full 17-row detected schema → exactly 6 inputs; missing → DBNull; LevelsID 0/unparsable; DefaultValue; DateTime.MinValue; invalid Constant; success/fallback/V2; 1/3/4 → 400/409/500; missing marker / UserName mapping → no DB call). 423 total green.
- Live (local Docker `LoginArchival`, snapshot-shaped `JsonElement` config): Create → Identifier = LoginID, row Flag 99 / LevelsID 1 / ReferenceID 7 / CreateBy 0; duplicate → 409; no role, role `0`, blank name → 400.

**Existing users (decision 2026-09-25, UTS only):** before creating, `ProvisionAsync` calls **GetSpName** with the sent LoginID.
- Not found (code 2) → Create as above.
- Found → **Update instead of Create** through **PatchSpName**: inputs = lookup parameter + the app's **PATCH** rows mapped from the incoming Entra user — mapped **only after GetSpName has found the user** (the Create payload, a private `List<OdbcParameter>` subclass, carries the Entra user because `ProvisionAsync` receives only the payload; nothing is mapped for Update when the user is new). **`@Flag` = the existing `Flag` returned by GetSpName** — the status of an existing account is kept (no reactivation / password reset as a side effect of matching). `usp_UpdateUTSArchivalUser` accepts `@Flag` 1 / 2 / 3 only (other values → code 1), so for the password states 4 / 98 / 99 (or NULL) `@Flag` is omitted, which also keeps the current Flag. A PATCH mapping to the lookup parameter or `@Flag` is ignored. Missing values → `DBNull` (= unchanged). POST-only parameters (`@ReferenceNo`, `@CreatedBy`) are not sent. Returns Identifier = existing `userId` (LoginID).
- Found but the app has no mapped PATCH rows → link only (no Update call).
- Lookup failure other than "not found" (e.g. code 4) → mapped error, no Create. Update failure → mapped error. PATCH rows but no PatchSpName → `ArgumentException`.
- No LoginID value sent → lookup skipped; Create lets the SP validate (code 1 → 400).
- Tests: `UTSArchivalSQLIntegrationTests.CreateExisting.cs`. Live: existing disabled user `P2T024452` → Name/LevelsID updated, `@Flag = 2` sent, **Flag stays 2**; existing Flag 99 user → no `@Flag`, **Flag stays 99**; new LoginID → created (Flag 99).
- This resolves the "existing UTS users → 409" blocker for UTS **without** changing the generic `NonSCIMUserProvider.QueryAsync`.

**Agent Instruction:** "Override only mapping and provisioning. Identifier returned must be LoginID, never a GUID."

**Checkpoint:** Unit tests: success returns Identifier = LoginID; codes 1 / 3 / 4 raise 400 / 409 / 500.

---

### Phase 4: Get (`GetAsync`) — [#150](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/150)

**Logic:**
- Override both `GetAsync` overloads: `ExecuteProcedureAsync(GetSpName, [<lookup param> = identifier])`; `ThrowIfFailed` (2 → 404).
- Map user row → `Core2EnterpriseUser`:
  - `Identifier` = `userId`, `UserName` = `username`, `DisplayName` = `displayName`, `Name.GivenName` = `firstName`
  - `ElectronicMailAddresses` = `email` (work, primary) when non-empty
  - `Active` = `Flag NOT IN (2, 3)` (1, 4, 98, 99 → active; 2 Disable, 3 Inactive → inactive)
  - `Roles` = single role `{ Value = LevelsID, Display = role }`
- Note: `usp_GetUTSArchivalUser` returns the reference description as unaliased column `Description`; do not depend on it.

✅ **Implemented (2026-09-25):** V1 `GetAsync` override (V2 delegates). Lookup input built by new `protected static CreateLookupParameter(AppConfig, identifier)` — typed/sized like the POST `UserName` row, value trimmed; reused by Update / Delete. GET uses the POST output markers. Code 2 → 404; success without a row → 404 (logged); empty identifier → `ArgumentNullException` before any DB call. Result columns are the SP contract (`userId`, `username`, `firstName`, `displayName`, `email`, `Flag`, `LevelsID`, `role`) held as constants in the UTS class; `Active = Flag ∉ {2, 3}` (NULL Flag → inactive); empty email / NULL LevelsID omitted. Tests: `UTSArchivalSQLIntegrationTests.Get.cs` (mapping, call shape, Flags 1/4/98/99/2/3, 2 → 404, no row → 404, 4 → 500, empty id, V2). 438 total green. Live (local Docker): Flag 1 / 4 / 99 → active, Flag 2 → inactive, unknown → 404; role value/display and email mapped.

**Agent Instruction:** "Map only the attributes listed. Unknown columns are ignored."

**Checkpoint:** Unit tests for active/inactive derivation across Flags 1, 2, 3, 4, 98, 99 and for 404.

---

### Phase 5: Update (`UpdateAsync`) — PATCH semantics and status — [#151](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/151)

**Problem:** `UpdateUserV4` applies the PATCH to a fresh `Core2EnterpriseUser`, so the integration cannot tell "`active` set to false" from "`active` not in the PATCH". Entra sends user disable **and** unassignment/out-of-scope as `PATCH active=false` (DELETE only arrives much later, if ever), so status must be handled in Update.

**Logic:**
- **Generic, minimal addition:** introduce a scoped `IPatchOperationContext` (`IReadOnlySet<string> PatchedPaths`) in `KN.KloudIdentity.Mapper`, populated in `UpdateUserV4.UpdateAsync` from `patchRequest.Operations` (path names, case-insensitive; a path-less `replace` op contributes the keys of its value object). Register in DI. No behaviour change for other integrations.
- Override both `UpdateAsync` overloads in `UTSArchivalSQLIntegration`:
  - Lookup parameter (e.g. `@LoginID`) = `resource.Identifier` (LoginID); any PATCH mapping to the same name is ignored (LoginID is immutable).
  - Every PATCH-mapped parameter whose source attribute is **not** in `PatchedPaths` → `DBNull` (SP treats NULL as unchanged). Constant mappings (e.g. `@UpdatedBy`) are always sent.
  - **Clearing values:** a *string* attribute that **is** in `PatchedPaths` but is removed / empty → `''` (not `DBNull`), so the SP stores `''` and the value is actually cleared (e.g. Entra removes the email). `@Name = ''` is still rejected by the SP (code 1) — correct.
  - Non-string attributes (`@LevelsID`, `@DOB`) cannot be cleared through the UTS SPs (NULL = unchanged); a removed value → `DBNull` and is logged at Warning ("clear not supported").
  - `@Flag`:
    - `active` not patched → `DBNull`
    - `active = true` → `1` (SP reactivates Flag 2/3 → Flag 4 with password reset)
    - `active = false` → `2` (Disable; SP also clears `IsLogin`)
  - `@Flag` is the only UTS-specific parameter added in code (derived from `active`, not mapped).
  - `ExecuteProcedureAsync(PatchSpName, …)`; `ThrowIfFailed`.
  - If after nulling nothing remains to change (all Direct-mapped values DBNull and `@Flag` DBNull), skip the call and log Information (avoids noise rows in `History`).

**Known SP defect (outside this task, tracked separately):** `usp_UpdateUTSArchivalUser` sets Flag 1 directly when the current Flag is 99/4/98, bypassing the forced first-login password change. The connector must therefore only send `@Flag = 1` when `active` is explicitly patched to `true` (never by default). Report the SP fix to PNB.

✅ **Implemented (2026-09-25):**
- **Generic:** `KN.KloudIdentity.Mapper/MapperCore/User/PatchOperationContext.cs` — `IPatchOperationContext` (scoped, registered in `ServiceExtension`): `Capture(operations)`, `IsPatched(sourceValue)` (add/replace/remove), `IsSet(sourceValue)` (add/replace). Patch paths and mapping `SourceValue`s are normalized to the same SCIM key via the `[DataMember]` names of `Core2EnterpriseUser` (e.g. `emails[type eq "work"].value` ↔ `ElectronicMailAddresses[0]:Value`; enterprise / KI extension attributes prefixed with their schema id; `name.givenName` does not cover `name.familyName`). **Path-less operations are ignored** (as `Core2EnterpriseUser.Apply` ignores them) — deviation from the original plan, which counted their value keys. `UpdateUserV4` only calls `patchOperationContext.Capture(patchRequest.Operations)` before `Apply` (new constructor parameter); no other handler change, no behaviour change for other integrations.
- **Regression audit of `IPatchOperationContext` (2026-09-25):** only `UpdateUserV4` (capture) and `UTSArchivalSQLIntegration` (read) use it; every other integration and handler is untouched. Scoped end-to-end (controller → `IProvider` → `NonSCIMUserProvider` → `UpdateUserV4` / factory / integrations, `Startup.cs:242-244`), so each HTTP request has its own context; SCIM bulk runs operations sequentially. Hardening: `Capture` never throws (an operation whose path cannot be parsed is skipped, `Core2EnterpriseUser.Apply` still reports it exactly as before), and `UpdateUserV4` calls `Reset()` in a `finally` after the update, so a capture never leaks into a later operation in the same scope (e.g. PUT after PATCH in one bulk request). Tests: invalid path, deserialized operations, reset; handler resets after success and after an integration exception, invalid path fails as before. 538 total green.
- **Base `SQLIntegration`:** `MapInputValue(attribute, resource, applyDefault)`, `IsProcedureInputRow`, `CreateInputParameter`, `IsStringType` extracted from `MapProcedureInputs` (behaviour unchanged).
- **UTS:** constructor takes `IPatchOperationContext`. PATCH rows → `MapUpdateInputs`: not patched → `DBNull`; patched but empty/removed → `''` for strings (clears), `DBNull` + warning for non-strings; Constants always sent; lookup parameter / `@Flag` rows ignored; every Direct `SourceValue` is still resolved, so an invalid path (e.g. `Role[0]:Value`) fails loudly. `UpdateAsync`: lookup = SCIM Identifier; `@Flag`: `active` set to false → 2; set to true → **1 only if the current Flag (GetSpName) is 2/3**, otherwise not sent (stricter than the original plan: guards the SP defect even when Entra re-sends `active=true` for an active account); `active` removed / not patched → not sent. Nothing to change → SP not called. Without a captured PATCH: values as mapped, nothing cleared, no `@Flag`.
- Tests: `MapperCore/User/PatchOperationContextTests.cs`, `UpdateUserV4Tests.UpdateAsync_CapturesPatchOperationsBeforeCallingIntegration`, `MapperCore/PNB/UTSArchivalSQLIntegrationTests.Update.cs`. 517 total green.
- Live (local Docker, same user): create (Flag 99) → replace displayName (only Name changed) → remove email (Email `''`) → replace role (LevelsID 3) → `active=true` on Flag 99 (no call) → `active=false` (**Flag 2**, IsLogin 0) → `active=true` (**Flag 4**, reactivated) → unmapped `title` (no call) → displayName `''` (400).
- **PUT** (`ReplaceUserV4`) — **supported (decision 2026-09-25):** handled as a full update through **PatchSpName**. `ReplaceUserV4` maps only PUT rows (the UTS app has none) → UTS `MapAndPreparePayloadAsync` returns an empty payload for an empty schema (Create / Update still reject an empty payload); `ReplaceAsync` (both overloads) then maps the **PATCH rows** from the full user in the body (missing → NULL = unchanged; lookup / `@Flag` rows ignored). Status **follows `active` in the body**: false → `@Flag 2`; true → `@Flag 1` only for a Flag 2/3 account (SP stores 4), otherwise not sent. A PUT body without `active` is read as false (SCIM default) and disables. Lookup = URL id (LoginID); `userName` in the body cannot rename. Tests: `UTSArchivalSQLIntegrationTests.Replace.cs`. **Generic fix:** `ReplaceUserV4.GetUserAttributes` (REST / SQL / SOAPEagle) now falls back to the **PATCH rows when the app has no PUT rows** (MgtPortal only maps POST / PATCH), mirroring `UpdateUserV4` (PATCH → PUT fallback); apps with PUT rows are unchanged. The UTS empty-payload path remains as a safeguard.

**Agent Instruction:** "Add `IPatchOperationContext` with no other changes to `UpdateUserV4` logic. In the UTS class, never send a non-NULL value for an attribute that was not patched."

**Checkpoint:** Unit tests: unpatched attrs → DBNull; patched-to-empty string → `''`; removed `@DOB` → DBNull + warning; `active=false` → `@Flag=2`; `active=true` → `@Flag=1`; no `active` → `@Flag` DBNull; empty patch → no SP call.

---

### Phase 6: Delete (`DeleteAsync`) — [#152](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/152)

**Logic:**
- Override both `DeleteAsync` overloads: `ExecuteProcedureAsync(DeleteSpName, [<lookup param> = identifier])`. (`@DeletedBy` defaults to 0 in the SP.)
- Code 3 ("already disabled") → treat as success (idempotent delete), log Information.
- Code 2 → 404 (Entra accepts 404 on DELETE). Others via `ThrowIfFailed`.

✅ **Implemented (2026-09-25, done before Phase 5):** V1 `DeleteAsync` override (V2 delegates). Lookup via `CreateLookupParameter` (POST `UserName` row) — the base `SQLIntegration.DeleteAsync` lookup (`SourceValue == "Identifier"`) is **not** used, so no `Identifier` mapping row is needed (the UTS app has none; adding one would also become a Create input). Identifier validated in the override (`DeleteUserV4.ValidateRequest` skips SQL apps). DELETE reuses the POST output markers; `@DeletedBy` not sent (SP default 0). Code 0 / 3 → success (3 logged as "already disabled"), 1 → 400, 2 → 404, 4 → 500; missing DeleteSpName → `ArgumentException`. Tests: `UTSArchivalSQLIntegrationTests.Delete.cs`. 467 total green. Live (local Docker): active user → Flag 2 / IsLogin 0; repeat DELETE → OK; unknown → 404.

**Agent Instruction:** "Override delete only; reuse `ExecuteProcedureAsync`."

**Checkpoint:** Unit tests for 0 → success, 3 → success, 2 → 404, 4 → 500.

---

### Phase 7: DI registration + AppId routing — [#153](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/153)

**Logic:**
- `ServiceExtension.cs`: add `services.AddScoped<IIntegrationBaseV2, UTSArchivalSQLIntegration>();` next to the PNB registrations (**done** 2026-09-25, next to `SQLIntegration`); register `IPatchOperationContext`.
- `Microsoft.SCIM.WebHostSample/appsettings.json` → `IntegrationMappings.AppIdToIntegration`: add `"<utsArchivalAppId>": "UTSArchivalSQLIntegration"` (exact class name; AppId from MgtPortal). Apply the same key in the prod App Configuration (`appconfig-iga-sea-prod`).

**Agent Instruction:** "One `AddScoped` line for the integration, one for the patch context, one appsettings key. Touch nothing else."

**Checkpoint:** Factory resolves `UTSArchivalSQLIntegration` for the UTS AppId and `SQLIntegration` for any other SQL AppId.

---

### Phase 8: Tests + local end-to-end verification — [#154](https://github.com/Kloudynet-Technologies/KloudIdentity/issues/154)

**Unit tests:** `KN.KloudIdentity.MapperTests/MapperCore/PNB/UTSArchivalSQLIntegrationTests.cs` (xUnit + Moq; see Part 4).

**Local E2E (mandatory — no PNB test environment):**
- Local reconstruction of `LoginArchival` in Docker SQL Server: `Himaya IGA Project - UTS Archival Provisioning SQL/local_setup/run_local.sh` (schema inferred from SPs, reference data seeded, the four SPs deployed unchanged).
- Point a local SCIM host at it (`localhost,1433`, ODBC Driver 17) and run the Entra-shaped Postman sequence: POST → GET → PATCH (name/email) → PATCH `active=false` → PATCH `active=true` → DELETE → DELETE again → GET unknown.
- Verify table state after each step (`Users.Flag`, `BridgeControl`, `History`).

**Checkpoint:** `dotnet test --filter "FullyQualifiedName~UTSArchivalSQLIntegration"` green; local E2E sequence produces the expected Flags (99 → 2 → 4 → 2) and HTTP codes.

---

## 🟦 PART 3: TECHNICAL CONSTRAINTS & GUARDRAILS

- **Do NOT** change the four UTS stored procedures or add objects to `LoginArchival`. The connector adapts to the delivered contract.
- **Do NOT** add a new `IntegrationMethods` enum value; routing is AppId-based (`IntegrationMethod = IntegrationMethods.SQL`).
- **Do NOT** hardcode SP names or parameter lists — SP names come from `SQLIntegrationDetails`, parameter names from the attribute mapping (`DestinationField`). Names are validated against `ParameterNamePattern` before they are written into the batch text; values are always bound as `OdbcParameter`. Output parameters come from the `Output:<Role>` marker rows (Constant). The only parameter name in code is `@Flag` (derived from `active`).
- **Do NOT** send `@Flag` unless `active` was explicitly patched. Never send `@Flag = 1` as a default.
- **Do NOT** treat "no exception" as success — success is `ResponseCode = 0` only.
- **Do NOT** log parameter values (names, emails, staff numbers), passwords or connection strings. Log AppId, CorrelationId, SP name, ResponseCode, ResponseMessage.
- **Connections:** `await using` for `OdbcConnection`, `OdbcCommand`, `DbDataReader`; one connection per operation; honour `CancellationToken`.
- **Coding Standards:** file-scoped namespaces; no `async void`; follow existing PNB class layout (`IntegrationMethods/PNB/`).
- **Scope of generic changes:** Phase 0, the base named-procedure engine (Phase 2, `protected`, opt-in) and `IPatchOperationContext` (Phase 5). `SQLIntegration` stays the generic SQL integration; UTS-specific rules (LoginID identifier, `@Flag`/`active`, PATCH semantics, Delete code 3, required `Output:ResponseCode`) live only in `UTSArchivalSQLIntegration`. Other SQL customers must see no behaviour change beyond the bug fixes.

---

## 🟩 PART 4: VERIFICATION & DEFINITION OF DONE

**Expected Output:**
- POST → SCIM user with `id` = LoginID (e.g. `ASNB9999`); UTS user created with Flag 99.
- GET → SCIM user with userName, displayName, email, `active` derived from Flag, single role.
- PATCH → only patched attributes sent; `active` toggles Flag 2 / reactivation (Flag 4).
- DELETE → Flag 2; repeat DELETE is idempotent.
- Any `ResponseCode ≠ 0` surfaces to Entra as the mapped HTTP error with the SP message logged.

**Unit Test Scenarios:**
- [ ] Phase 0: `SQLIntegration` resolves via factory as `IIntegrationBaseV2`; existing SQL tests pass; GET reads the first row
- [ ] Batch text: named parameters emitted in order with matching `?` count; outputs declared from marker rows with the type from `DestinationType`, passed `OUTPUT`, returned via trailing SELECT aliased by role
- [ ] Output markers: Constant `Output:ResponseCode` is never bound as an input value; Direct with the same value behaves identically; Get/Delete reuse POST markers; no `Output:ResponseCode` mapped → `InvalidOperationException`; renamed output parameter (e.g. `@RetCode` marked `Output:ResponseCode`) works without code change
- [ ] Parameter names: taken from mapping; malformed input or output name (e.g. `@Name = 1; --`), duplicate name, or same name as input and output → `ArgumentException`; invalid SP name → `ArgumentException`
- [ ] Lookup parameter resolved from the POST `UserName` mapping; missing mapping → `InvalidOperationException`
- [ ] Result parsing: user row + status row; status row only (validation early return); no status row → exception
- [ ] Create: 0 → Identifier = LoginID; 1 → 400; 3 → 409; 4 → 500
- [ ] Create: `@LevelsID` "0"/unparsable → DBNull; `DateTime.MinValue` → DBNull; blank strings → DBNull
- [ ] Detected rows: full 17-row POST schema with 5 inputs + 2 output markers mapped → exactly 5 input and 2 output parameters emitted; `DefaultValue "N/A"` never bound; PATCH rows ignored on Create
- [ ] Get: 2 → 404; Flag 1/4/98/99 → active; Flag 2/3 → inactive; role mapped
- [ ] Update: unpatched attributes → DBNull; patched-to-empty string → `''` (clears); removed non-string → DBNull + warning; `active` false/true/absent → `@Flag` 2/1/DBNull; empty patch → no SP call
- [ ] Mapped required parameter with no source value → sent as DBNull → SP code 1 → 400 (never SQL error 201)
- [ ] Delete: 0 and 3 → success; 2 → 404; 4 → 500
- [ ] Routing: UTS AppId → `UTSArchivalSQLIntegration`; other SQL AppId → `SQLIntegration`

**Local E2E:** sequence in Phase 8 passes against the Docker `LoginArchival` reconstruction.

---

## ⬜ PART 5: IMPACT & DEPENDENCIES

**Impacted Components:**
- `KN.KloudIdentity.Mapper` — `SQLIntegration.cs` (V2 + fixes), new `PNB/UTSArchivalSQLIntegration.cs`, new `IPatchOperationContext`, `UpdateUserV4.cs` (populate context), `ServiceExtension.cs`
- `Microsoft.SCIM.WebHostSample` — `appsettings.json` routing key (+ prod App Configuration)
- `KN.KloudIdentity.MapperTests` — updated SQL tests, new UTS test file

**No changes to:** `IntegrationMethods` enum, MgtPortal, Metaverse schema detection, LocalAgent, UTS stored procedures.

**Dependent Tasks / Open Items:**
- **`HttpRequestTypes` numbering (fixed 2026-09-25):** snapshots carry `HttpRequestType` as a **number** in MgtPortal numbering (`NONE=0, POST=1, PUT=2, PATCH=3`); SCIM had `POST=0, PUT=1, PATCH=2`, so POST rows were read as PUT and PATCH rows as GET (verified in local `ScimConnectorDb`: app `himayaarchivaluat` has 17 rows with `1` and 17 with `3`). `NotDefined` was added as value 0 in `KN.KloudIdentity.Mapper.Domain/Mapping/HttpRequestTypes.cs`, aligning POST/PUT/PATCH. Side effect: `default(HttpRequestTypes)` is now `NotDefined` (two tests that relied on default = POST were updated). SCIM `GET=4 / DELETE=5` still differ from MgtPortal (`NULL=4`) — not used in attribute mappings. Impacts **all** apps (REST/SOAP/SQL) — verify existing app mappings after deploy.
- **UTS AppId:** local snapshot contains `himayaarchivaluat` (17 POST + 17 PATCH rows) — likely the UAT AppId for `IntegrationMappings.AppIdToIntegration` (Phase 7); confirm the prod AppId.
- **ReferenceNo uniqueness:** `usp_CreateUTSArchivalUser` also returns code 3 (→ 409) when another user has the same `ReferenceNo` (staff number). The pre-create lookup only checks LoginID, so a new LoginID with an existing staff number still fails with 409 — expected, surfaces as a provisioning error in Entra.
- **PII in handler logs (generic, found in Phase 3):** `CreateUserV4.ExecuteGenericUserCreationLogicAsync` logs `JsonConvert.SerializeObject(payload)` at Information — for SQL that is the `OdbcParameter` list **including values** (verified: email, name, staff number appear). Violates Part 3 logging guardrail; fix in the handler (e.g. log parameter names only for SQL) — needs a decision since it is shared code.
- ~~**Q1 – LoginID rule**~~ **Resolved:** LoginID = Entra `userName`, supplied through the Entra provisioning attribute mapping (SCIM `userName` → `UserName` → `@LoginID`). The source attribute feeding `userName` in Entra must be stable, since LoginID is immutable.
- ~~**Q2 – Status mapping**~~ **Resolved (2026-09-25):** KloudIdentity sends `@Flag = 2` for `active=false` and `@Flag = 1` for `active=true` (only when the account is Flag 2/3). `usp_UpdateUTSArchivalUser` stores **Flag 4** on reactivation (default password reset + forced change at next login, mirrors the UTS admin UI) — accepted as correct; no connector change. Read-back: 1/4/98/99 → active, 2/3 → inactive.
- **Q3 – ReferenceNo / LevelsID:** confirm staff number source attribute and format; role source (group → LevelsID) and precedence (single role per user).
- **SP defect to report to PNB:** `usp_UpdateUTSArchivalUser` Flag 1 on a Flag 99/4/98 user bypasses the forced password change.
- **Network:** SCIM pod in AKS must reach `10.40.11.6:1433` (NSG / firewall / route); ODBC Driver 17 must be present in the SCIM container image (not installed by the current dockerfiles — verify the deployed image).
- **Driver name check:** `DbConnectionFactory` matches `OdbcConnection.Driver` against `SQLGlobalConstants.SqlServerDrivers` (`LIBMSODBCSQL.17.SO`); confirm the Linux driver file name in the image (e.g. `libmsodbcsql-17.x.so.y`) matches, or extend the list. The named-procedure engine (Phase 2, used by UTS) does not use `DbConnectionFactory` and already accepts versioned names (`LIBMSODBCSQL*` / `MSODBCSQL*`); only the base positional `{CALL}` path depends on the exact list.
- **Credentials:** PNB SR for the IGA SQL login (EXECUTE on the four SPs only).
- **Optional generic follow-up (Metaverse / MgtPortal):** return `p.is_output` from `QUERY_SCHEMA_DETECTION_SQLSERVER` and have MgtPortal pre-fill OUTPUT rows as `MappingType = Constant`, `SourceValue = "Output"` (admin then only adds the role, e.g. `Output:ResponseCode`); stop seeding `DefaultValue = "N/A"`. Do **not** filter OUTPUT params out of detection — the marker rows are needed. Not required for this task: the admin sets the markers manually and the UTS class already ignores `"N/A"`.
- **Existing UTS users — CONFIRMED BLOCKER (Phase 4):** Entra matches existing accounts via `GET /Users?filter=userName eq "…"`, but `NonSCIMUserProvider.QueryAsync` (`Microsoft.SCIM.WebHostSample/Provider/NonSCIMUserProvider.cs:130-331`) parses the filter and **always returns an empty list** (`results = new List<Resource>()`, lookup commented out, `@TODO: Implement query`) — for every app. Entra therefore never matches pre-existing UTS users and sends POST → UTS returns code 3 → **409** for everyone already in `LoginArchival`. Fix option (generic, needs decision): for `userName eq X`, resolve the app and call `GetUserV4` with identifier X (valid for UTS because SCIM id = LoginID = userName); 404 → empty result. **Resolved for UTS (2026-09-25):** Create looks the LoginID up through GetSpName first and updates an existing user instead of creating it (Phase 3). The generic `QueryAsync` gap remains for other apps.

**Anti-Drift Log:**
- *Wrapper SPs in `LoginArchival`* (THROW on non-zero code + GUID↔LoginID map table) were considered and rejected: requires new objects in PNB's production-only database via SR, and still leaves the generic connector bugs.
- *Hardcoded per-operation parameter whitelists* were dropped: parameter names already live in the attribute mapping (`DestinationField`); a strict name-format regex gives the same injection protection without duplicating config in code.
- *Hardcoded `@ResponseCode` / `@ResponseMessage` names* were replaced by `Output:<Role>` marker rows (MappingType Constant) in the attribute mapping. A plain `@output` marker was considered but rejected — it identifies outputs but not which one is the status code, which would force hardcoded names again. Adding the markers to the Entra provisioning schema was rejected — Entra would send them as SCIM attributes.
- *Reading OUTPUT params via `ParameterDirection.Output`* was replaced by the `DECLARE … EXEC … SELECT` batch: deterministic with `System.Data.Odbc`, and names parameters explicitly.
- *Deactivation via DELETE only* was rejected: Entra sends disable/unassign as `PATCH active=false`, so status must be handled in Update (requires `IPatchOperationContext`).
