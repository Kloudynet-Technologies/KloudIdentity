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
    /// Name of the outbound target field whose value must be reshaped from a
    /// comma-separated string into a primitive JSON array.
    /// </summary>
    private const string ReportsFieldName = "reports";

    /// <summary>
    /// Delimiter used by Entra to pack multiple report codes into a single string value.
    /// </summary>
    private const char CsvDelimiter = ',';

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
    /// Builds the outbound payload using the standard mapping pipeline, then reshapes the
    /// <c>reports</c> field from a comma-separated string (e.g. "PAC01A,PAC01B") into a
    /// primitive JSON string array (e.g. ["PAC01A","PAC01B"]) as required by the ASNB Bo LOB app.
    /// All other fields are left exactly as produced by the base mapping.
    /// </summary>
    public override async Task<dynamic> MapAndPreparePayloadAsync(
        IList<AttributeSchema> schema,
        Core2EnterpriseUser resource,
        CancellationToken cancellationToken = default)
    {
        //  Build the base payload via the standard mapping pipeline.
        var payload = await base.MapAndPreparePayloadAsync(schema, resource, cancellationToken);
        JObject jPayload = payload as JObject ?? JObject.FromObject(payload);

        // Locate the reports node. Nothing to do if it is absent.
        var reportsToken = jPayload.SelectToken(ReportsFieldName);
        if (reportsToken == null || reportsToken.Type == JTokenType.Null)
        {
            Log.Debug(
                "[ASNBBoIntegration] No '{Field}' field present in payload for resource {ResourceId}; skipping CSV transform.",
                ReportsFieldName, resource.Identifier);

            return jPayload;
        }

        //Only transform when the base produced a scalar string.
        // If it is already an array (or any non-string token), leave it untouched to stay idempotent.
        if (reportsToken.Type != JTokenType.String)
        {
            return jPayload;
        }

        var codes = reportsToken.Value<string>()!
            .Split(CsvDelimiter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Empty/whitespace input yields an empty array (agreed default behavior).
        reportsToken.Replace(new JArray(codes));

        Log.Information(
            "[ASNBBoIntegration] Converted '{Field}' CSV string into an array of {Count} code(s) for resource {ResourceId}.",
            ReportsFieldName, codes.Length, resource.Identifier);

        //Return the reshaped payload.
        return jPayload;
    }
}
