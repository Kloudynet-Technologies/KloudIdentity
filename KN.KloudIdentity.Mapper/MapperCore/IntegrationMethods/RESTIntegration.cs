using System;
using System.Web.Http;
using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.SCIM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore;

/// <summary>
/// Integration logic implementation for REST.
/// </summary>
public class RESTIntegration : IIntegrationBase
{
    private readonly IAuthContext _authContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public IntegrationMethods IntegrationMethod { get; init; }

    public RESTIntegration(IAuthContext authContext,
                            IHttpClientFactory httpClientFactory,
                            IConfiguration configuration)
    {
        IntegrationMethod = IntegrationMethods.REST;

        _authContext = authContext;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    /// <summary>
    /// Gets the authentication token asynchronously.
    /// </summary>
    /// <param name="config"></param>
    /// <param name="direction"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async Task<dynamic> GetAuthenticationAsync(AppConfig config, SCIMDirections direction = SCIMDirections.Outbound, CancellationToken cancellationToken = default)
    {
        return await _authContext.GetTokenAsync(config, direction);
    }

    /// <summary>
    /// Attribute mapping and prepares the payload asynchronously.
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="resource"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async Task<dynamic> MapAndPreparePayloadAsync(IList<AttributeSchema> schema, Core2EnterpriseUser resource, CancellationToken cancellationToken = default)
    {
        var payload = JSONParserUtilV2<Resource>.Parse(schema, resource);

        return await Task.FromResult(payload);
    }

    /// <summary>
    /// Provisions the user asynchronously to LOB application.
    /// </summary>
    /// <param name="payload"></param>
    /// <param name="appConfig"></param>
    /// <param name="correlationID"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="HttpRequestException">When an error occurred during provisioning</exception>
    public virtual async Task ProvisionAsync(dynamic payload, AppConfig appConfig, string correlationID, CancellationToken cancellationToken = default)
    {
        var userURIs = appConfig.UserURIs.FirstOrDefault(x => x.SCIMDirection == SCIMDirections.Outbound);

        var token = await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound);

        var httpClient = _httpClientFactory.CreateClient();

        Utils.HttpClientExtensions.SetAuthenticationHeaders(httpClient, appConfig.AuthenticationMethodOutbound, appConfig.AuthenticationDetails, token, SCIMDirections.Outbound);

        using var response = await httpClient.PostAsJsonAsync(
            userURIs?.Post,
            payload as JObject
        );

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Error creating user: {response.StatusCode} - {response.ReasonPhrase}"
            );
        }
    }

    /// <summary>
    /// Validates the payload asynchronously before been sent to LOB app.
    /// </summary>
    /// <param name="payload"></param>
    /// <param name="correlationID"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Validation status and error messages</returns>
    public virtual Task<(bool, string[])> ValidatePayloadAsync(dynamic payload, string correlationID, CancellationToken cancellationToken = default)
    {
        // No payload validation required for REST integration. Always return true.
        return Task.FromResult((true, Array.Empty<string>()));
    }

    public async Task<Core2EnterpriseUser> GetAsync(string identifier, AppConfig appConfig, string correlationID, CancellationToken cancellationToken = default)
    {
        var userURIs = appConfig.UserURIs.Where(x => x.SCIMDirection == SCIMDirections.Outbound).FirstOrDefault();

        if (userURIs != null && userURIs.Get != null)
        {
            var token = await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound, cancellationToken);

            var client = _httpClientFactory.CreateClient();
            Utils.HttpClientExtensions.SetAuthenticationHeaders(client, appConfig.AuthenticationMethodOutbound, appConfig.AuthenticationDetails, token, SCIMDirections.Outbound);
            var response = await client.GetAsync(DynamicApiUrlUtil.GetFullUrl(userURIs.Get.ToString(), identifier));

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var user = JsonConvert.DeserializeObject<JObject>(content);
                if (user == null)
                {
                    throw new NotFoundException($"User not found with identifier: {identifier}");
                }

                var core2EntUsr = new Core2EnterpriseUser();

                string urnPrefix = _configuration["urnPrefix"]!;

                string idField = GetFieldMapperValue(appConfig, "Identifier", urnPrefix);
                string usernameField = GetFieldMapperValue(appConfig, "UserName", urnPrefix);

                core2EntUsr.Identifier = GetValueCaseInsensitive(user, idField);
                core2EntUsr.UserName = GetValueCaseInsensitive(user, usernameField);

                // await CreateLogAsync(_appConfig, identifier, correlationID);

                return core2EntUsr;
            }
            else
            {
                throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
            }
        }
        else
        {
            throw new ApplicationException("GET API for users is not configured.");
        }
    }

    private string GetFieldMapperValue(AppConfig appConfig, string fieldName, string urnPrefix)
    {
        var field = appConfig.UserAttributeSchemas.FirstOrDefault(f => f.SourceValue == fieldName);
        if (field != null)
        {
            return field.DestinationField.Remove(0, urnPrefix.Length);
        }
        else
        {
            throw new NotFoundException(fieldName + " field not found in the user schema.");
        }
    }

    private string GetValueCaseInsensitive(JObject jsonObject, string propertyName)
    {
        var property = jsonObject.Properties()
            .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

        return property!.Value.ToString();
    }
}
