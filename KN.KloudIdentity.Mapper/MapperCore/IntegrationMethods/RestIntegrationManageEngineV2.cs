using System.Text;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore;

public class RestIntegrationManageEngineV2 : RESTIntegration
{
    private readonly IConfiguration _configuration;
    private readonly AppSettings _appSettings;

    public RestIntegrationManageEngineV2(IAuthContext authContext, IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IKloudIdentityLogger logger,
        IOptions<AppSettings> appSettings) :
        base(authContext, httpClientFactory, configuration, appSettings, logger)
    {
        _configuration = configuration;
        IntegrationMethod = IntegrationMethods.REST;
        _appSettings = appSettings.Value;
    }

    public override async Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema,
        Core2EnterpriseUser resource,
        CancellationToken cancellationToken = default)
    {
        // For ManageEngine, we might have specific payload preparation logic
        // Implement any ManageEngine specific payload mapping here if needed

        var payload = await base.MapAndPreparePayloadAsync(schema, resource, cancellationToken);

        return new
        {
            user = payload
        };
    }

    protected override async Task<HttpClient> CreateHttpClientAsync(AppConfig appConfig, SCIMDirections direction,
        CancellationToken cancellationToken = default)
    {
        var client = await base.CreateHttpClientAsync(appConfig, direction, cancellationToken);

        // Add any ManageEngine specific headers or configurations here if needed

        var manageEngineAPIKey = _configuration["ManageEngine:APIKey"];
        if (string.IsNullOrEmpty(manageEngineAPIKey))
        {
            Log.Error("ManageEngine API Key is not configured. Please add 'ManageEngine:APIKey' to the application configuration.");
            throw new InvalidOperationException("ManageEngine API Key is not configured. Please add 'ManageEngine:APIKey' to the application configuration.");
        }

        client.DefaultRequestHeaders.Add("AuthToken", manageEngineAPIKey);

        return client;
    }

    /// <summary>
    /// Prepare HTTP content in application/x-www-form-urlencoded format for ManageEngine
    /// </summary>
    /// <param name="payload">The JSON payload to be sent</param>
    /// <param name="contentType">
    /// This parameter is ignored as ManageEngine requires application/x-www-form-urlencoded format.
    /// It exists only for interface compliance.
    /// </param>
    /// <returns></returns>
    protected override HttpContent PrepareHttpContent(JObject payload, string? contentType)
    {
        var encodedJson = Uri.EscapeDataString(payload.ToString(Formatting.None));
        var formData = $"input_data={encodedJson}";

        return new StringContent(formData, Encoding.UTF8, "application/x-www-form-urlencoded");
    }

    protected override async Task<string> GetGeneratedIdentifierAsync(dynamic payload, HttpResponseMessage response, AppConfig appConfig)
    {
        // For ManageEngine, the identifier might be in a different format or location
        var responseContent = await response.Content.ReadAsStringAsync();
        var jsonResponse = JsonConvert.DeserializeObject<JObject>(responseContent);

        var idValue = jsonResponse?["user"]?["id"]?.ToString();
        if (!string.IsNullOrEmpty(idValue))
        {
            return idValue;
        }

        throw new InvalidOperationException("Unable to determine the generated identifier from ManageEngine response.");
    }

    protected override string GetValueCaseInsensitive(JObject? jsonObject, string propertyName)
    {
        if (jsonObject == null)
            throw new InvalidOperationException("JObject can't be null.");

        return base.GetValueCaseInsensitive(jsonObject["user"] as JObject, propertyName);
    }
}
