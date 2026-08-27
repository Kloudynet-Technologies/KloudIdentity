using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore;

public class ASNBBoIntegration : RESTIntegrationV4
{
    /// <summary>
    /// Name of the outbound target field that receives ROLE_-prefixed appRoleAssignment values.
    /// </summary>
    private const string RolesFieldName = "roles";

    /// <summary>
    /// Name of the outbound target field that receives non-ROLE_-prefixed appRoleAssignment values.
    /// </summary>
    private const string ReportsFieldName = "reports";

    /// <summary>
    /// Name of the outbound target field indicating whether the user holds the Refund Backoffice role.
    /// </summary>
    private const string IsCheckerFieldName = "isChecker";

    /// <summary>
    /// Prefix that identifies an appRoleAssignment value as a role (as opposed to a report code).
    /// </summary>
    private const string RolePrefix = "ROLE_";

    /// <summary>
    /// App role value for "Refund Backoffice User"; its presence flags the user as a checker.
    /// </summary>
    private const string RefundBoRoleValue = "ROLE_REFUND_BO";

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
    /// the Refund Backoffice role (<c>ROLE_REFUND_BO</c>) is present. All other fields are left
    /// exactly as produced by the base mapping.
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
            .Where(v => v.StartsWith(RolePrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var reports = roleValues.Except(roles).ToList();

        var isChecker = roles.Any(v => string.Equals(v, RefundBoRoleValue, StringComparison.OrdinalIgnoreCase));

        jPayload[RolesFieldName] = new JArray(roles);
        jPayload[ReportsFieldName] = new JArray(reports);
        jPayload[IsCheckerFieldName] = isChecker;

        Log.Information(
            "[ASNBBoIntegration] Split {Total} appRoleAssignment value(s) into {RoleCount} role(s) and {ReportCount} report(s) for resource {ResourceId}. IsChecker={IsChecker}.",
            roleValues.Count, roles.Count, reports.Count, resource.Identifier, isChecker);

        //Return the reshaped payload.
        return jPayload;
    }
}
