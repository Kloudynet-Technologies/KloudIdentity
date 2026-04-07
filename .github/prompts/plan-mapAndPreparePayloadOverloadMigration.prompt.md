## Plan: Low-Impact AppConfig Signature Migration

Maintain two `MapAndPreparePayloadAsync` signatures in `IIntegrationBase` in parallel: legacy and AppConfig-aware. Concrete classes implement only the overloads they need (with bridges where useful), and callers choose the appropriate overload based on whether `AppConfig` is already available. This gives the least disruption while enabling SOAP and future integrations to use `AppConfig` immediately.

**Steps**

1. Introduce a new AppConfig overload in `IIntegrationBase` and keep the existing method.
2. Keep both interface overloads active in parallel; do not break existing callers.
3. In concrete integrations, implement the relevant overload explicitly:
   1. `SOAPIntegration` uses the AppConfig overload as canonical.
   2. Other integrations can keep legacy-only behavior initially and add AppConfig overloads as needed.
4. Add class-level bridging only where useful (for example, old overload calling new or vice versa) to avoid duplicate mapping logic.
5. Update primary orchestrator callsites to pass `appConfig` first in V2 user flows, because appConfig is already available there.
6. Update derived overrides that call `base.MapAndPreparePayloadAsync` to route to the intended overload (with or without AppConfig).
7. Adjust unit tests in the SQL integration area only where the AppConfig overload is intentionally exercised; keep legacy tests untouched for compatibility.
8. Add migration tracking:
   1. Document which integrations support the AppConfig overload.
   2. Optionally mark legacy overload as obsolete later, once adoption is complete.

**Relevant files**

- `d:\KloudIdentity\SCIM-Connector Service\KloudIdentity\KN.KloudIdentity.Mapper\MapperCore\IntegrationMethods\IIntegrationBase.cs` — add the new overload and deprecation annotations; define forwarding contract.
- `d:\KloudIdentity\SCIM-Connector Service\KloudIdentity\KN.KloudIdentity.Mapper\MapperCore\IntegrationMethods\SOAPIntegration.cs` — make AppConfig overload primary and keep old method as compatibility bridge.
- `d:\KloudIdentity\SCIM-Connector Service\KloudIdentity\KN.KloudIdentity.Mapper\MapperCore\IntegrationMethods\RESTIntegration.cs` — add new overload that reuses old logic initially.
- `d:\KloudIdentity\SCIM-Connector Service\KloudIdentity\KN.KloudIdentity.Mapper\MapperCore\IntegrationMethods\RESTIntegrationV2.cs` — align override signature path with AppConfig overload.
- `d:\KloudIdentity\SCIM-Connector Service\KloudIdentity\KN.KloudIdentity.Mapper\MapperCore\IntegrationMethods\RESTManageEngineIntegration.cs` — update `base.MapAndPreparePayloadAsync` forwarding to include `appConfig`.
- `d:\KloudIdentity\SCIM-Connector Service\KloudIdentity\KN.KloudIdentity.Mapper\MapperCore\IntegrationMethods\LinuxIntegration.cs` — compatibility overload.
- `d:\KloudIdentity\SCIM-Connector Service\KloudIdentity\KN.KloudIdentity.Mapper\MapperCore\IntegrationMethods\AS400Integration.cs` — compatibility overload.
- `d:\KloudIdentity\SCIM-Connector Service\KloudIdentity\KN.KloudIdentity.Mapper\MapperCore\IntegrationMethods\SQLIntegration.cs` — compatibility overload.
- `d:\KloudIdentity\SCIM-Connector Service\KloudIdentity\KN.KloudIdentity.Mapper\MapperCore\User\CreateUserV2.cs` — pass `appConfig` to map call.
- `d:\KloudIdentity\SCIM-Connector Service\KloudIdentity\KN.KloudIdentity.Mapper\MapperCore\User\UpdateUserV2.cs` — pass `appConfig` to map call.
- `d:\KloudIdentity\SCIM-Connector Service\KloudIdentity\KN.KloudIdentity.Mapper\MapperCore\User\ReplaceUserV2.cs` — pass `appConfig` to map call.
- `d:\KloudIdentity\SCIM-Connector Service\KloudIdentity\KN.KloudIdentity.MapperTests\SqlIntegration\SQLIntegrationTest.Mapping.cs` — keep tests green during transition; selectively adopt new overload in migration-focused tests.

**Verification**

1. Build solution and verify no interface-implementation compile errors after adding overload and compatibility bridges.
2. Run mapper core tests with focus on SQL mapping tests and any integration method tests.
3. Execute at least one end-to-end user provisioning path for SOAP and one REST path to verify both new and compatibility paths.
4. Confirm no `NotSupportedException` from SOAP mapping when called through interface in V2 user flows.
5. Search for remaining legacy signature-only callsites and ensure they are either intentionally preserved or migrated.

**Decisions**

- Chosen strategy: parallel overload maintenance (legacy + AppConfig) with caller-selected usage.
- Included scope: `IIntegrationBase` payload-mapping overload evolution and immediate V2 user flow callsites that already have `appConfig`.
- Excluded scope: forced migration of all legacy callsites and immediate removal of the legacy overload.
- Assumption: overload resolution remains explicit at callsites to avoid ambiguity and behavior regressions.

**Further Considerations**

1. Deprecation severity recommendation: start with warning (`[Obsolete(message, false)]`), then move to error in a later release.
2. If external assemblies implement `IIntegrationBase`, publish migration notes before final legacy removal.
3. Consider introducing a small mapping context type in the future if more parameters are expected beyond `AppConfig`.
