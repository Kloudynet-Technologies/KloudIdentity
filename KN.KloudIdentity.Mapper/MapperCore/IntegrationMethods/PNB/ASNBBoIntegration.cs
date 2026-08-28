using KN.KI.LogAggregator.Library.Abstractions;
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

        var reports = roleValues.Except(roles).ToList();

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
    /// and matching <see cref="ExtensionAttributeKIUserBase.ExtensionAttribute5"/> (everything
    /// from the "ASNB" marker word onward, e.g. "Cawangan ASNB Segamat" / "Branch ASNB Segamat"
    /// both -> "ASNB Segamat") against each returned branch's <c>name</c>. Returns an empty
    /// string (rather than throwing) when the marker/keyword is missing or no branch matches.
    /// </summary>
    private async Task<string> ResolveBranchIdAsync(
        Core2EnterpriseUser resource,
        AppConfig appConfig,
        CancellationToken cancellationToken)
    {
        var rawBranchValue = resource.KIExtension.ExtensionAttribute5?.Trim();
        var asnbIndex = rawBranchValue?.IndexOf(AppConstant.AsnbBoAsnbMarker, StringComparison.OrdinalIgnoreCase) ?? -1;
        var branchKeyword = asnbIndex >= 0 ? rawBranchValue![asnbIndex..].Trim() : null;

        if (string.IsNullOrWhiteSpace(branchKeyword))
        {
            Log.Warning(
                "[ASNBBoIntegration] ExtensionAttribute5 ('{RawValue}') has no '{Marker}' marker for resource {ResourceId}; leaving branchid empty.",
                rawBranchValue, AppConstant.AsnbBoAsnbMarker, resource.Identifier);
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

        var baseUrl = new Uri(createEndpoint).GetLeftPart(UriPartial.Authority);
        var formDataUrl = $"{baseUrl}{AppConstant.AsnbBoReferenceFormDataPath}";

        var client = await CreateHttpClientAsync(appConfig, SCIMDirections.Outbound, cancellationToken);
        var response = await client.GetAsync(formDataUrl, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Log.Error(
                "[ASNBBoIntegration] Branch reference API call failed. AppId: {AppId}, StatusCode: {StatusCode}, Response: {Response}",
                appConfig.AppId, response.StatusCode, body);
            throw new HttpRequestException($"Branch reference API call failed: {response.StatusCode} - {body}");
        }

        var branches = JObject.Parse(body)["data"]?["branches"] as JArray ?? [];

        var branchCode = branches
            .FirstOrDefault(b => (b["name"]?.ToString() ?? string.Empty)
                .Contains(branchKeyword, StringComparison.OrdinalIgnoreCase))
            ?["code"]?.ToString();

        if (string.IsNullOrWhiteSpace(branchCode))
        {
            Log.Warning(
                "[ASNBBoIntegration] No branch match found for keyword '{BranchKeyword}' (resource {ResourceId}); leaving branchid empty.",
                branchKeyword, resource.Identifier);
            return string.Empty;
        }

        return branchCode;
    }
}
