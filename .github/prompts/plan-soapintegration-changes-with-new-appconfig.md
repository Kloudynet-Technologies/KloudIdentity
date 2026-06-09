# Plan: SOAP Integration Changes for New AppConfig Structure

## Context

The AppConfig structure has changed. SOAP XML templates are no longer stored as a separate
`AppConfig.SOAPTemplates` collection. They now live directly on each `ActionStep` as a `template`
property. Every SOAP action contains exactly one `ActionStep`.

Old shape:
```
AppConfig.SOAPTemplates[ { Template, Action: SOAPActions } ]
AppConfig.Actions[ { ActionSteps[ { EndPoint, HttpVerb, UserAttributeSchemas } ] } ]
```

New shape:
```
AppConfig.Actions[ { ActionSteps[ { EndPoint, HttpVerb, template, UserAttributeSchemas } ] } ]
```

Relevant enums:
- `ActionNames`: GET=1, CREATE=2, EDIT=3, DELETE=4
- `SOAPActions`: Create, Update, Delete, Get  (becomes unused — see Step 2)

---

## Step 1 — Domain: Add `Template` to `ActionStep`

**File:** `KN.KloudIdentity.Mapper.Domain/Application/ActionStep.cs`

Add one property:
```csharp
public string? Template { get; init; }
```

JSON key in AppConfig is lowercase `template` — verify that the deserializer is
case-insensitive (it is: `AppConfigSnapshotRepository` uses `PropertyNameCaseInsensitive = true`),
so no converter is needed.

---

## Step 2 — Domain: Remove `SOAPTemplates` from `AppConfig` and delete dead types

**File:** `KN.KloudIdentity.Mapper.Domain/Application/AppConfig.cs`

Remove:
```csharp
public ICollection<SOAPTemplate>? SOAPTemplates { get; set; }
```

Also remove the `Validate()` guard that references `UserAttributeSchemas` for non-REST methods
only if it becomes incorrect; otherwise leave it.

**Files to delete** once nothing references them:
- `KN.KloudIdentity.Mapper.Domain/Application/SOAPTemplate.cs`
- `KN.KloudIdentity.Mapper.Domain/Mapping/SOAPActions.cs`

> Do not delete until Steps 3–7 are complete and the build is green.

---

## Step 3 — SOAPIntegration: Remove `ResolveSoapTemplate` and `MapHttpVerbToSoapAction`

**File:** `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/SOAPIntegration.cs`

These two helpers exist solely to look up a template from `AppConfig.SOAPTemplates`:

- `protected static SOAPTemplate ResolveSoapTemplate(AppConfig, SOAPActions)` — **delete**
- `protected static SOAPActions MapHttpVerbToSoapAction(HttpVerbs, string)` — **delete**

Before deleting, fix every call site first (Steps 4 and 5).

---

## Step 4 — SOAPIntegration: Fix ActionStep overloads that still use `ResolveSoapTemplate`

**File:** `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/SOAPIntegration.cs`

### 4a. `GetAsync(identifier, appConfig, actionStep, correlationId, cancellationToken)` — lines ~170–186

Replace:
```csharp
var soapAction = MapHttpVerbToSoapAction(actionStep.HttpVerb, "GET");
SOAPTemplate template = ResolveSoapTemplate(appConfig, soapAction);
// ...
var soapPayload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(template.Template, attributes, resource);
```

With:
```csharp
var template = actionStep.Template
    ?? throw new InvalidOperationException(
        $"ActionStep {actionStep.StepOrder} has no template for GET. AppId: {appConfig.AppId}");
var soapPayload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(template, attributes, resource);
```

### 4b. `DeleteAsync(identifier, appId, appConfig, actionStep, correlationId, cancellationToken)` — lines ~213–228

Replace:
```csharp
var soapAction = MapHttpVerbToSoapAction(actionStep.HttpVerb, "DELETE");
SOAPTemplate template = ResolveSoapTemplate(appConfig, soapAction);
// ...
var soapPayload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(template.Template, attributes, resource);
```

With:
```csharp
var template = actionStep.Template
    ?? throw new InvalidOperationException(
        $"ActionStep {actionStep.StepOrder} has no template for DELETE. AppId: {appConfig.AppId}");
var soapPayload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(template, attributes, resource);
```

---

## Step 5 — SOAPIntegration: Replace `MapAndPreparePayloadAsync` with ActionStep-aware overload

The existing `MapAndPreparePayloadAsync(schema, resource, appConfig)` reads
`appConfig.SOAPTemplates?.FirstOrDefault()`. Since the template is now on `ActionStep`,
add a new overload and retire the old one.

**File:** `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/SOAPIntegration.cs`

### 5a. Add new overload

```csharp
public virtual async Task<dynamic> MapAndPreparePayloadAsync(
    IList<AttributeSchema> schema,
    Core2EnterpriseUser resource,
    AppConfig appConfig,
    ActionStep actionStep,
    CancellationToken cancellationToken = default)
{
    var template = actionStep.Template
        ?? throw new InvalidOperationException(
            $"ActionStep {actionStep.StepOrder} has no template. AppId: {appConfig.AppId}");

    string payload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(template, schema, resource);
    return await Task.FromResult(payload);
}
```

### 5b. Change the old AppConfig-only overload to throw

The 3-param overload `MapAndPreparePayloadAsync(schema, resource, appConfig)` is no longer
usable for SOAP. Change its body to:
```csharp
throw new NotSupportedException(
    "SOAP payload mapping requires an ActionStep. Use the overload that accepts ActionStep.");
```

This keeps the interface contract intact while making misuse visible at runtime immediately.

### 5c. Add the new overload to the interface

**File:** `KN.KloudIdentity.Mapper/MapperCore/IIntegrationBase.cs` (or wherever
`MapAndPreparePayloadAsync(schema, resource, AppConfig)` is declared)

Add:
```csharp
Task<dynamic> MapAndPreparePayloadAsync(
    IList<AttributeSchema> schema,
    Core2EnterpriseUser resource,
    AppConfig appConfig,
    ActionStep actionStep,
    CancellationToken cancellationToken = default);
```

---

## Step 6 — EagleSOAPIntegration: Switch to ActionStep-based template access

**File:** `KN.KloudIdentity.Mapper/MapperCore/IntegrationMethods/EagleSOAPIntegration.cs`

### 6a. Override the new `MapAndPreparePayloadAsync` overload (Step 5a)

Remove the current override that reads `appConfig.SOAPTemplates?.FirstOrDefault()` and
patches a new `AppConfig`. Replace it with an override of the new ActionStep signature:

```csharp
public override async Task<dynamic> MapAndPreparePayloadAsync(
    IList<AttributeSchema> schema,
    Core2EnterpriseUser resource,
    AppConfig appConfig,
    ActionStep actionStep,
    CancellationToken cancellationToken = default)
{
    var template = actionStep.Template
        ?? throw new InvalidOperationException(
            $"ActionStep {actionStep.StepOrder} has no template. AppId: {appConfig.AppId}");

    var injected = template.Replace("{{CorrelationId}}", Guid.NewGuid().ToString(), StringComparison.Ordinal);
    string payload = SOAPParserUtil<Core2EnterpriseUser>.BuildPayload(injected, schema, resource);
    return await Task.FromResult(payload);
}
```

No need to patch `AppConfig with { SOAPTemplates = ... }` anymore.

### 6b. `DeleteAsync` ActionStep overload — lines ~180–202

Replace:
```csharp
var template = ResolveSoapTemplate(appConfig, MapHttpVerbToSoapAction(actionStep.HttpVerb, "DELETE"));
// ...
var injected = template.Template.Replace("{{CorrelationId}}", ...);
```

With:
```csharp
var template = actionStep.Template
    ?? throw new InvalidOperationException(
        $"ActionStep {actionStep.StepOrder} has no template for DELETE. AppId: {appConfig.AppId}");
var injected = template.Replace("{{CorrelationId}}", Guid.NewGuid().ToString(), StringComparison.Ordinal);
```

### 6c. Legacy overloads (no ActionStep) — `DeleteAsync` line ~154, `ReplaceAsync` line ~93, `UpdateAsync` line ~125

These read from `appConfig.UserURIs` and `appConfig.SOAPTemplates`. They are only reachable via
`ExecuteGenericUser*` paths, which for SOAP/SOAPEagle are never taken in V4. Make each throw:
```csharp
throw new NotSupportedException("Use the ActionStep overload for SOAPEagle operations.");
```

---

## Step 7 — ProvisioningBase: Remove `GetMappingConfigForSoapAction`

**File:** `KN.KloudIdentity.Mapper/MapperCore/Outbound/ProvisioningBase.cs`

Delete the entire `GetMappingConfigForSoapAction(AppConfig, SOAPActions)` method (lines ~86–102).
Its only job was to create a scoped `AppConfig` with one filtered `SOAPTemplate` — no longer needed.

---

## Step 8 — CreateUserV4: Use ActionStep-aware payload mapping

**File:** `KN.KloudIdentity.Mapper/MapperCore/User/CreateUserV4.cs`

### 8a. `ExecuteMultistepForRESTAsync` — lines ~77–80

Replace:
```csharp
var mappingConfig = GetMappingConfigForSoapAction(_appConfig, SOAPActions.Create);
var config = _appConfig.IntegrationMethodOutbound == IntegrationMethods.SOAP ? mappingConfig : _appConfig;
var payload = await integrationOp.MapAndPreparePayloadAsync(userAttributes, resource, config);
```

With:
```csharp
var payload = (_appConfig.IntegrationMethodOutbound == IntegrationMethods.SOAP
               || _appConfig.IntegrationMethodOutbound == IntegrationMethods.SOAPEagle)
    ? await integrationOp.MapAndPreparePayloadAsync(userAttributes, resource, _appConfig, step, CancellationToken.None)
    : await integrationOp.MapAndPreparePayloadAsync(userAttributes, resource, _appConfig);
```

`step` is already in scope from the `foreach` loop.

### 8b. `ExecuteGenericUserCreationLogicAsync` — lines ~171–174

This path is unreachable for SOAP since `ExecuteAsync` always routes SOAP through
`ExecuteMultistepForRESTAsync`. Replace the SOAP-specific `GetMappingConfigForSoapAction` call
with a guard:
```csharp
if (_appConfig.IntegrationMethodOutbound == IntegrationMethods.SOAP
    || _appConfig.IntegrationMethodOutbound == IntegrationMethods.SOAPEagle)
    throw new NotSupportedException("SOAP requires action steps. Check AppConfig.Actions.");
```

Or simply remove the SOAP/SOAPEagle branch from the `GetUserAttributes` switch and let it fall
through to the default (which returns all attributes) — harmless since this path is dead for SOAP.

---

## Step 9 — ReplaceUserV4: Use ActionStep-aware payload mapping

**File:** `KN.KloudIdentity.Mapper/MapperCore/User/ReplaceUserV4.cs`

### 9a. `ExecuteMultistepForRESTAsync` — lines ~75–79

Replace:
```csharp
var mappingConfig = GetMappingConfigForSoapAction(_appConfig, SOAPActions.Update);
var config = _appConfig.IntegrationMethodOutbound == IntegrationMethods.SOAP ? mappingConfig : _appConfig;
var payload = await integrationOp.MapAndPreparePayloadAsync(attributes, resource, config);
```

With:
```csharp
var payload = (_appConfig.IntegrationMethodOutbound == IntegrationMethods.SOAP
               || _appConfig.IntegrationMethodOutbound == IntegrationMethods.SOAPEagle)
    ? await integrationOp.MapAndPreparePayloadAsync(attributes, resource, _appConfig, step, CancellationToken.None)
    : await integrationOp.MapAndPreparePayloadAsync(attributes, resource, _appConfig);
```

### 9b. `ExecuteGenericUserReplaceLogicAsync` — line ~110

Same as Step 8b — remove the `GetMappingConfigForSoapAction` call; guard or simplify.

---

## Step 10 — Delete dead files

Once the build is green with no remaining references:

1. Delete `KN.KloudIdentity.Mapper.Domain/Application/SOAPTemplate.cs`
2. Delete `KN.KloudIdentity.Mapper.Domain/Mapping/SOAPActions.cs`

Run a final grep for `SOAPTemplate` and `SOAPActions` to confirm zero references.

---

## Step 11 — Update Tests

Search for `SOAPTemplates` across all test projects and update each occurrence:

**Pattern to find:** `SOAPTemplates = [` or `SOAPTemplates =`

**What to do:** Remove the `SOAPTemplates` initializer from the `AppConfig` setup and instead set
`Template = "..."` on the relevant `ActionStep` in `ActionSteps`.

Key test files likely affected (search `*Tests*.cs` for `SOAPTemplates`):
- `DeleteUserV4Test.cs`
- `ReplaceUserV4Tests.cs`
- Any `SOAPIntegration` unit tests
- Any `EagleSOAPIntegration` unit tests

For each test that previously used `GetMappingConfigForSoapAction` indirectly, verify the test
still covers the correct path.

---

## Execution Order Summary

| Step | File(s) | Change Type |
|------|---------|-------------|
| 1 | `ActionStep.cs` | Add `Template` property |
| 2 | `AppConfig.cs` | Remove `SOAPTemplates` |
| 3 | `SOAPIntegration.cs` | Delete `ResolveSoapTemplate` + `MapHttpVerbToSoapAction` |
| 4 | `SOAPIntegration.cs` | Fix `GetAsync` + `DeleteAsync` ActionStep overloads |
| 5 | `SOAPIntegration.cs` + interface | New `MapAndPreparePayloadAsync` ActionStep overload |
| 6 | `EagleSOAPIntegration.cs` | Switch all template access to `actionStep.Template` |
| 7 | `ProvisioningBase.cs` | Delete `GetMappingConfigForSoapAction` |
| 8 | `CreateUserV4.cs` | Use ActionStep-aware payload mapping |
| 9 | `ReplaceUserV4.cs` | Use ActionStep-aware payload mapping |
| 10 | `SOAPTemplate.cs`, `SOAPActions.cs` | Delete files |
| 11 | Test projects | Update all `SOAPTemplates` → `ActionStep.Template` |

Build after each step. Steps 1 and 2 will cause compile errors that Steps 3–9 resolve.
It is safe to do Steps 1–9 in one pass before building if preferred.
