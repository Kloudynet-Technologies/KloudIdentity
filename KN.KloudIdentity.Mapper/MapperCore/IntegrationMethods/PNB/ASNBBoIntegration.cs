using System.Globalization;
using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore;

public class ASNBBoIntegration : RESTIntegrationV4
{
    private bool _deprovisionedDuringUpdate;

    public ASNBBoIntegration(
        IAuthContext authContext,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IKloudIdentityLogger logger,
        IOptions<AppSettings> appSettings)
        : base(authContext, httpClientFactory, configuration, logger, appSettings)
    {
        IntegrationMethod = IntegrationMethods.REST;
    }

    /// <summary>
    /// Before running the normal EDIT action step, checks whether the user is inactive
    /// (<see cref="Core2EnterpriseUser.Active"/> is <c>false</c>) or their leave date
    /// (<see cref="ExtensionAttributeKIUserBase.ExtensionAttribute4"/>, formatted <c>dd/MM/yyyy</c>
    /// by the Entra attribute mapping) is strictly in the past. If either is true, the update is
    /// skipped and the app's configured <c>DELETE</c>/<c>USER</c> action step(s) are invoked instead,
    /// deprovisioning the user in the LOB app rather than pushing a routine attribute update to it.
    /// Note: <see cref="Core2EnterpriseUser.Active"/> is a non-nullable bool that defaults to
    /// <c>false</c> whenever the incoming SCIM PATCH doesn't explicitly touch "active" (the common
    /// case for a routine attribute-only update) — so a PATCH that never mentions "active" will also
    /// be treated as inactive here and trigger deprovisioning.
    /// A request may run this check once per instance (one per Update HTTP call, DI-scoped) even when
    /// the app has multiple EDIT action steps configured, so DELETE is only ever invoked once.
    /// </summary>
    public override async Task UpdateAsync(
        dynamic payload,
        Core2EnterpriseUser resource,
        string appId,
        AppConfig appConfig,
        ActionStep actionStep,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (_deprovisionedDuringUpdate)
        {
            return;
        }

        if (!resource.Active || IsLeaveDateInPast(resource))
        {
            Log.Information(
                "[ASNBBoIntegration] Deprovision condition met for resource {ResourceId} (Active={Active}, LeaveDate={LeaveDate}); deprovisioning via DELETE action step(s) instead of updating. AppId: {AppId}, CorrelationID: {CorrelationID}",
                resource.Identifier, resource.Active, resource.KIExtension.ExtensionAttribute4, appId, correlationId);

            await DeprovisionAsync(resource.Identifier, appId, appConfig, correlationId, cancellationToken);
            _deprovisionedDuringUpdate = true;
            return;
        }

        await base.UpdateAsync((object)payload, resource, appId, appConfig, actionStep, correlationId, cancellationToken);
    }

    private static bool IsLeaveDateInPast(Core2EnterpriseUser resource)
    {
        var rawLeaveDate = resource.KIExtension.ExtensionAttribute4?.Trim();
        if (string.IsNullOrWhiteSpace(rawLeaveDate))
        {
            return false;
        }

        if (!DateTime.TryParseExact(
                rawLeaveDate,
                AppConstant.AsnbBoLeaveDateFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var leaveDate))
        {
            Log.Warning(
                "[ASNBBoIntegration] ExtensionAttribute4 ('{RawValue}') is not a valid '{Format}' date for resource {ResourceId}; skipping deprovision-on-update check.",
                rawLeaveDate, AppConstant.AsnbBoLeaveDateFormat, resource.Identifier);
            return false;
        }

        return leaveDate.Date < DateTime.UtcNow.Date;
    }

    private async Task DeprovisionAsync(
        string identifier,
        string appId,
        AppConfig appConfig,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var deleteSteps = appConfig.Actions?
            .Where(a => a is { ActionName: ActionNames.DELETE, ActionTarget: ActionTargets.USER })
            .SelectMany(a => a.ActionSteps)
            .OrderBy(s => s.StepOrder)
            .ToList() ?? [];

        if (deleteSteps.Count == 0)
        {
            throw new InvalidOperationException(
                $"No DELETE action step(s) configured for app {appId}; cannot deprovision user {identifier} during update.");
        }

        foreach (var deleteStep in deleteSteps)
        {
            await DeleteAsync(identifier, appId, appConfig, deleteStep, correlationId, cancellationToken);
        }
    }

    /// <summary>
    /// The ASNB Bo deprovisioning endpoint is a single fixed "lock" URL (no identifier in the
    /// path) — the user to deprovision is identified by an
    /// <c>{ "id": "&lt;identifier&gt;", "action": "LOCK" }</c> JSON body posted to it.
    /// This replaces the base <see cref="RESTIntegrationV4.DeleteAsync"/> behavior, which issues a
    /// bodyless HTTP DELETE against an endpoint with the identifier substituted into the path.
    /// </summary>
    public override async Task DeleteAsync(
        string identifier,
        string appId,
        AppConfig appConfig,
        ActionStep actionStep,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actionStep);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionStep.EndPoint);

        if (actionStep.HttpVerb != HttpVerbs.POST)
        {
            throw new NotSupportedException(
                $"Right now action step with StepOrder {actionStep.StepOrder}, HttpVerb {actionStep.HttpVerb}, EndPoint '{actionStep.EndPoint}' is not supported for delete operation. Expected HttpVerb: POST.");
        }

        var body = new JObject
        {
            [AppConstant.AsnbBoDeleteIdFieldName] = identifier,
            [AppConstant.AsnbBoLockActionFieldName] = AppConstant.AsnbBoLockActionValue
        };
        var content = PrepareHttpContent(body, null);

        var client = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, actionStep.EndPoint) { Content = content };
        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Log.Error(
                "[ASNBBoIntegration] Deprovisioning failed. AppId: {AppId}, CorrelationID: {CorrelationID}, StatusCode: {StatusCode}, Response: {ResponseBody}",
                appConfig.AppId, correlationId, response.StatusCode, responseBody);

            throw new HttpRequestException($"Error deleting user: {response.StatusCode} - {responseBody}");
        }

        _ = CreateLogAsync(appConfig.AppId,
            "Delete User",
            $"User deleted successfully for the id {identifier}",
            LogType.Deprovision,
            LogSeverities.Information,
            correlationId);
    }

    /// <summary>
    /// Builds the outbound payload using the standard mapping pipeline, then derives
    /// <c>roles</c>, <c>reports</c> and <c>isChecker</c> from the resource's raw appRoleAssignments
    /// (<see cref="Core2EnterpriseUser.Roles"/>): values starting with <c>ROLE_</c> become
    /// <c>roles</c>, everything else becomes <c>reports</c>, and <c>isChecker</c> is true when
    /// the Refund Backoffice role (<c>ROLE_REFUND_BO</c>) is present. Also sets <c>isExpire</c>
    /// to true when <see cref="ExtensionAttributeKIUserBase.ExtensionAttribute4"/> has a value,
    /// false otherwise. All other fields are left exactly as produced by the base mapping.
    /// </summary>
    public override async Task<dynamic> MapAndPreparePayloadAsync(
        IList<AttributeSchema> schema,
        Core2EnterpriseUser resource,
        CancellationToken cancellationToken = default)
    {
        //  Build the base payload via the standard mapping pipeline.
        var payload = await base.MapAndPreparePayloadAsync(schema, resource, cancellationToken);
        JObject jPayload = payload as JObject ?? JObject.FromObject(payload);

        var roleValues = resource.Roles?
            .Select(r => r.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList() ?? [];

        var roles = roleValues
            .Where(v => v.StartsWith(AppConstant.AsnbBoRolePrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var reports = roleValues
            .Where(v => !v.StartsWith(AppConstant.AsnbBoRolePrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var isChecker = roles.Any(v => string.Equals(v, AppConstant.AsnbBoRefundBoRoleValue, StringComparison.OrdinalIgnoreCase));

        var isExpire = !string.IsNullOrWhiteSpace(resource.KIExtension.ExtensionAttribute4);

        jPayload[AppConstant.AsnbBoRolesFieldName] = new JArray(roles);
        jPayload[AppConstant.AsnbBoReportsFieldName] = new JArray(reports);
        jPayload[AppConstant.AsnbBoIsCheckerFieldName] = isChecker;
        jPayload[AppConstant.AsnbBoIsExpireFieldName] = isExpire;

        Log.Information(
            "[ASNBBoIntegration] Split {Total} appRoleAssignment value(s) into {RoleCount} role(s) and {ReportCount} report(s) for resource {ResourceId}. IsChecker={IsChecker}, IsExpire={IsExpire}.",
            roleValues.Count, roles.Count, reports.Count, resource.Identifier, isChecker, isExpire);

        //Return the reshaped payload.
        return jPayload;
    }

    /// <summary>
    /// Appconfig-aware overload (invoked by CreateUserV4/ReplaceUserV4 for REST integrations).
    /// Adds <c>hqorbranch</c> and <c>branchid</c> on top of the payload produced by
    /// <see cref="MapAndPreparePayloadAsync(IList{AttributeSchema}, Core2EnterpriseUser, CancellationToken)"/>.
    /// HQ users get the fixed <see cref="AppConstant.AsnbBoHqBranchId"/>; branch users are resolved by matching
    /// <see cref="ExtensionAttributeKIUserBase.ExtensionAttribute5"/> against the branch reference
    /// data returned by the app's <c>/api/v1/reference/formData</c> endpoint.
    /// </summary>
    public override async Task<dynamic> MapAndPreparePayloadAsync(
        IList<AttributeSchema> schema,
        Core2EnterpriseUser resource,
        AppConfig appConfig,
        CancellationToken cancellationToken = default)
    {
        var payload = await MapAndPreparePayloadAsync(schema, resource, cancellationToken);
        JObject jPayload = payload as JObject ?? JObject.FromObject(payload);

        var hqOrBranch = resource.KIExtension.ExtensionAttribute2?.Trim() ?? string.Empty;
        jPayload[AppConstant.AsnbBoHqOrBranchFieldName] = hqOrBranch;

        var branchId = string.Equals(hqOrBranch, AppConstant.AsnbBoHqValue, StringComparison.OrdinalIgnoreCase)
            ? AppConstant.AsnbBoHqBranchId
            : await ResolveBranchIdAsync(resource, appConfig, cancellationToken);

        jPayload[AppConstant.AsnbBoBranchIdFieldName] = branchId;

        Log.Information(
            "[ASNBBoIntegration] Resolved hqorbranch '{HqOrBranch}' to branchid '{BranchId}' for resource {ResourceId}.",
            hqOrBranch, branchId, resource.Identifier);

        return jPayload;
    }

    /// <summary>
    /// Resolves the branch code for a non-HQ user by calling the app's reference data endpoint
    /// and matching the full <see cref="ExtensionAttributeKIUserBase.ExtensionAttribute5"/> against
    /// each branch's <c>ofcLocations</c> (contains, either direction, case-insensitive). Only when no
    /// <c>ofcLocations</c> entry matches does it fall back to comparing branches with empty
    /// <c>ofcLocations</c> by <c>name</c> (full value, or the text from the "ASNB" marker onward).
    /// Returns an empty string (rather than throwing) when no branch matches.
    /// </summary>
    private async Task<string> ResolveBranchIdAsync(
        Core2EnterpriseUser resource,
        AppConfig appConfig,
        CancellationToken cancellationToken)
    {
        var rawBranchValue = resource.KIExtension.ExtensionAttribute5?.Trim();
        if (string.IsNullOrWhiteSpace(rawBranchValue))
        {
            Log.Warning(
                "[ASNBBoIntegration] ExtensionAttribute5 is empty for resource {ResourceId}; leaving branchid empty.",
                resource.Identifier);
            return string.Empty;
        }       

        var createEndpoint = appConfig.Actions?
            .FirstOrDefault(a => a.ActionTarget == ActionTargets.USER && a.ActionName == ActionNames.CREATE)
            ?.ActionSteps?.OrderBy(s => s.StepOrder).FirstOrDefault()?.EndPoint;

        if (string.IsNullOrWhiteSpace(createEndpoint))
        {
            throw new InvalidOperationException(
                $"No CREATE action step endpoint configured for app {appConfig.AppId}; cannot derive the branch reference API base URL.");
        }

        if (!Uri.TryCreate(createEndpoint, UriKind.Absolute, out var createEndpointUri))
        {
            throw new InvalidOperationException(
                $"CREATE action step endpoint '{createEndpoint}' configured for app {appConfig.AppId} is not a valid absolute URI; cannot derive the branch reference API base URL.");
        }

        var baseUrl = createEndpointUri.GetLeftPart(UriPartial.Authority);
        var formDataUrl = $"{baseUrl}{AppConstant.AsnbBoReferenceFormDataPath}";

        var client = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, cancellationToken);
        using var response = await client.GetAsync(formDataUrl, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Log.Error(
                "[ASNBBoIntegration] Branch reference API call failed. AppId: {AppId}, StatusCode: {StatusCode}, Response: {Response}",
                appConfig.AppId, response.StatusCode, body);
            throw new HttpRequestException($"Branch reference API call failed: {response.StatusCode} - {body}");
        }

        var branches = (JObject.Parse(body)["data"]?["branches"] as JArray ?? []).ToList();
        string branchValue = rawBranchValue;

        // Pass 1: match the full ExtensionAttribute5 against any branch's ofcLocations.
        // Exact (case-insensitive) matches win; a partial match is accepted only if it identifies one branch.
        var branchCode = ResolveUniqueBranchCode(
            branches,
            b => GetOfcLocations(b).Any(loc => loc.Equals(branchValue, StringComparison.OrdinalIgnoreCase)),
            b => GetOfcLocations(b).Any(loc =>
                loc.Contains(branchValue, StringComparison.OrdinalIgnoreCase) ||
                branchValue.Contains(loc, StringComparison.OrdinalIgnoreCase)),
            rawBranchValue, resource.Identifier);

        // Pass 2: name fallback, only for branches without ofcLocations.
        if (string.IsNullOrWhiteSpace(branchCode))
        {
            var asnbIndex = rawBranchValue.IndexOf(AppConstant.AsnbBoAsnbMarker, StringComparison.OrdinalIgnoreCase);
            var branchKeyword = asnbIndex >= 0 ? rawBranchValue[asnbIndex..].Trim() : null;

            var nameCandidates = branches.Where(b => GetOfcLocations(b).Count == 0).ToList();

            branchCode = ResolveUniqueBranchCode(
                nameCandidates,
                b =>
                {
                    var name = (b["name"]?.ToString() ?? string.Empty).Trim();
                    return name.Equals(branchValue, StringComparison.OrdinalIgnoreCase) ||
                           (branchKeyword != null && name.Equals(branchKeyword, StringComparison.OrdinalIgnoreCase));
                },
                b =>
                {
                    var name = b["name"]?.ToString() ?? string.Empty;
                    return name.Contains(branchValue, StringComparison.OrdinalIgnoreCase) ||
                           (branchKeyword != null && name.Contains(branchKeyword, StringComparison.OrdinalIgnoreCase));
                },
                rawBranchValue, resource.Identifier);
        }

        if (string.IsNullOrWhiteSpace(branchCode))
        {
            Log.Warning(
                "[ASNBBoIntegration] No branch match found for ExtensionAttribute5 '{RawValue}' (resource {ResourceId}); leaving branchid empty.",
                rawBranchValue, resource.Identifier);
            return string.Empty;
        }

        return branchCode;
    }

    /// <summary>
    /// Returns the branch code for a unique exact match; otherwise for a unique partial match.
    /// Returns null when nothing matches or the match is ambiguous (never depends on response order).
    /// </summary>
    private static string? ResolveUniqueBranchCode(
        IReadOnlyList<JToken> branches,
        Func<JToken, bool> exactMatch,
        Func<JToken, bool> partialMatch,
        string rawValue,
        object? resourceId)
    {
        foreach (var (stage, predicate) in new[] { ("exact", exactMatch), ("partial", partialMatch) })
        {
            var codes = branches
                .Where(predicate)
                .Select(b => b["code"]?.ToString())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (codes.Count == 1)
            {
                return codes[0];
            }

            if (codes.Count > 1)
            {
                Log.Warning(
                    "[ASNBBoIntegration] Ambiguous {Stage} branch match for ExtensionAttribute5 '{RawValue}' (resource {ResourceId}): {Codes}; leaving branchid empty.",
                    stage, rawValue, resourceId, string.Join(", ", codes));
                return null;
            }
        }

        return null;
    }

    private static List<string> GetOfcLocations(JToken branch) =>
        (branch["ofcLocations"] as JArray)?
            .Select(l => l?.ToString().Trim() ?? string.Empty)
            .Where(l => l.Length > 0)
            .ToList() ?? [];
}
