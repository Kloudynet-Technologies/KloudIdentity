## Agent Execution Prompt: SOAP Authentication Build Plan

Implement SOAP authentication support in incremental phases with strict scope control and test-first validation.

### Objective

Add three SOAP-compatible auth modes without breaking existing REST, SQL, or current SOAP behavior.

1. Basic and NTLM transport auth.
2. WS-Security header injection (`UsernameToken` plain text in phase 1).
3. Token-based auth in HTTP Authorization header, custom HTTP headers, and custom SOAP header.

### Hard Constraints

1. Preserve backward compatibility of existing `AuthenticationDetails` payloads.
2. Do not refactor unrelated modules.
3. Keep existing public contracts stable unless strictly required.
4. Do not remove current auth methods.
5. Stop after each phase and report changed files and rationale.

### Scope Decisions (Locked)

1. WS-Security first profile: `UsernameToken` plain text with optional timestamp toggle.
2. NTLM supports both explicit credentials and default machine credentials via config flag.
3. Token placement supports all three locations in phase 1:

- HTTP Authorization header
- Custom HTTP headers
- Custom SOAP header

### Primary Files

1. `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/SOAPIntegration.cs`
2. `KN.KloudIdentity.Mapper/Utils/HttpClientExtensions.cs`
3. `KN.KloudIdentity.Mapper/Utils/ServiceExtension.cs`
4. `KN.KloudIdentity.Mapper/Auth/AuthContextV1.cs`
5. `KN.KloudIdentity.Mapper/Auth/IAuthContext.cs`
6. `KN.KloudIdentity.Mapper/Auth/IAuthStrategy.cs`
7. `KN.KloudIdentity.Mapper.Domain/Application/AppConfig.cs`
8. `KN.KloudIdentity.Mapper.Domain/Authentication/AuthenticationMethods.cs`
9. `KN.KloudIdentity.MapperTests/SOAPIntegration/SOAPIntegrationUnitTests.cs`

### Phase Plan

#### Phase 1: Foundation and Contracts

Implement only foundational seams and config contracts.

Tasks:

1. Add SOAP-specific auth application seam (for example `ISoapAuthApplier`) that can:

- modify `HttpRequestMessage` headers
- modify SOAP XML envelope/header
- support transport credential decisions for HTTP handlers

2. Add typed SOAP auth option models with backward-compatible deserialization fallback from `AuthenticationDetails`.
3. Keep `IAuthStrategy` focused on token or key retrieval; do not force WS-Security logic into token strategies.

Done Criteria:

1. Build passes.
2. No behavior change for existing REST, SQL, and current SOAP auth flows.
3. Unit tests added for config parsing and seam selection.

Stop and report after Phase 1.

#### Phase 2: Implement Auth Modes

Implement concrete auth behavior.

Tasks:

1. Basic and NTLM transport auth:

- Basic path uses existing header logic.
- NTLM path configures credentials (`HttpClientHandler.Credentials`) and supports explicit or default credentials.

2. WS-Security injection:

- create `<soap:Header>` if absent
- inject `<wsse:Security>` and required namespaces
- inject UsernameToken plain text
- optional timestamp based on config

3. Token placement:

- Authorization header
- custom HTTP headers
- custom SOAP header fragment with token placeholder substitution

4. Define deterministic precedence when multiple placements are enabled.

Done Criteria:

1. New tests validate each auth mode independently.
2. Mixed-mode test validates combined token placements.
3. SOAP body mapping remains unchanged except intended header injection.

Stop and report after Phase 2.

#### Phase 3: Wire SOAP Integration and DI

Integrate the new auth pipeline and ensure runtime resolution.

Tasks:

1. Refactor `SOAPIntegration` request flow to use `HttpRequestMessage` and SOAP auth appliers.
2. Remove hardcoded bearer assignment.
3. Reuse shared custom header handling where applicable.
4. Register missing DI services:

- `BearerAuthStratergy`
- `SOAPIntegration`
- SOAP auth appliers

5. Verify `IntegrationBaseFactory` still resolves mapped integration correctly.

Done Criteria:

1. SOAP integration tests pass with new auth paths.
2. Existing SOAP tests remain green.
3. DI composition verifies all required auth services are registered.

Stop and report after Phase 3.

#### Phase 4: Compatibility and Regression Safety

Finalize regression protection and documentation notes.

Tasks:

1. Add/extend tests for:

- NTLM explicit vs default credential selection
- WS-Security XML structure
- token placement locations
- integration factory resolution

2. Validate no regressions in REST and SQL auth flows.
3. Add concise migration notes for optional new SOAP auth fields.

Done Criteria:

1. Targeted test suites pass.
2. No changed behavior for legacy configs.
3. Summary includes remaining known risks.

### Non-Goals (Phase 1 Rollout)

1. WS-Security digest or X.509 signature/canonicalization.
2. Large auth framework redesign.
3. Global enum cleanup beyond what is necessary for SOAP implementation.

### Mandatory Validation Commands

Run and report results for relevant tests before each phase handoff.

1. `dotnet test KN.KloudIdentity.MapperTests/KN.KloudIdentity.MapperTests.csproj --filter SOAPIntegration`
2. If no filterable tests exist, run: `dotnet test KN.KloudIdentity.MapperTests/KN.KloudIdentity.MapperTests.csproj`

### Output Format For Each Phase Handoff

Return exactly these sections:

1. `Phase Completed`
2. `Files Changed`
3. `Behavior Added`
4. `Tests Executed`
5. `Compatibility Check`
6. `Risks/Follow-ups`

### Execution Start Instruction

Start with Phase 1 only. Do not begin Phase 2 until Phase 1 handoff is produced.
