using System;
using System.Text;
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
        var userURIs = appConfig.UserURIs.FirstOrDefault();

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

    /// <summary>
    /// List all users from LOB application asynchronously.
    /// </summary>
    /// <param name="identifier">Unique identifier of the user</param>
    /// <param name="appConfig">App configurations</param>
    /// <param name="correlationID">Correlation ID</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="NotFoundException">When user not found in the LOB app</exception>
    /// <exception cref="HttpResponseException">When user not found in the LOB app</exception>
    /// <exception cref="ApplicationException">When GET API is notCore2EnterpriseUser resource, string appId,</exception>
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

    /// <summary>
    /// Replaces a user in the LOB application asynchronously.
    /// </summary>
    /// <param name="payload">Payload to send to the replace operation</param>
    /// <param name="resource">Resource to be replaced</param>
    /// <param name="appConfig">Application config</param>
    /// <param name="correlationID">Correlation ID</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="HttpRequestException"></exception>
    public async Task ReplaceAsync(JObject payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationID)
    {
        // Obtain authentication token.
        var token = await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound);

        var httpClient = _httpClientFactory.CreateClient();

        // Set headers based on authentication method.
        Utils.HttpClientExtensions.SetAuthenticationHeaders(httpClient, appConfig.AuthenticationMethodOutbound, appConfig.AuthenticationDetails, token, SCIMDirections.Outbound);

        var userURIs = appConfig.UserURIs.Where(x => x.SCIMDirection == SCIMDirections.Outbound).FirstOrDefault();

        HttpResponseMessage response;

        if (userURIs!.Put != null)
        {
            var apiPath = DynamicApiUrlUtil.GetFullUrl(userURIs.Put!.ToString(), resource.Identifier);

            response = await httpClient.PutAsJsonAsync(apiPath, payload);
        }
        else if (userURIs.Patch != null)
        {
            var apiPath = DynamicApiUrlUtil.GetFullUrl(userURIs.Patch.ToString(), resource.Identifier);
            var jsonPayload = payload.ToString();
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            response = await httpClient.PatchAsync(apiPath, content);
        }
        else
        {
            throw new ArgumentNullException("PUTAPIForUsers and PATCHAPIForUsers cannot both be null or empty");
        }

        // Check if the request was successful; otherwise, throw an exception.
        if (response != null && !response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Error updating user: {response.StatusCode} - {response.ReasonPhrase}"
            );
        }
    }

    /// <summary>
    /// Updates a user in the LOB application asynchronously.
    /// </summary>
    /// <param name="payload"></param>
    /// <param name="resource"></param>
    /// <param name="appConfig"></param>
    /// <param name="correlationID"></param>
    /// <returns></returns>
    /// <exception cref="HttpRequestException"></exception>
    public async Task UpdateAsync(JObject payload, Core2EnterpriseUser resource, AppConfig appConfig, string correlationID)
    {
        // Obtain authentication token.
        var token = await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound);

        var userURIs = appConfig.UserURIs.FirstOrDefault(x => x.SCIMDirection == SCIMDirections.Outbound);

        var httpClient = _httpClientFactory.CreateClient();

        Utils.HttpClientExtensions.SetAuthenticationHeaders(httpClient, appConfig.AuthenticationMethodOutbound, appConfig.AuthenticationDetails, token, SCIMDirections.Outbound);

        var apiPath = DynamicApiUrlUtil.GetFullUrl(userURIs!.Patch!.ToString(), resource.Identifier);

        var jsonPayload = payload.ToString();

        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        using (var response = await httpClient.PatchAsync(apiPath, content))
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Error updating user: {response.StatusCode} - {response.ReasonPhrase}"
                );
            }
        }
    }

    /// <summary>
    /// Deletes a resource asynchronously.
    /// </summary>
    /// <param name="identifier"></param>
    /// <param name="appConfig"></param>
    /// <param name="correlationID"></param>
    /// <returns></returns>
    /// <exception cref="HttpRequestException"></exception>
    public async Task DeleteAsync(string identifier, AppConfig appConfig, string correlationID)
    {
        var userURIs = appConfig.UserURIs.FirstOrDefault(x => x.SCIMDirection == SCIMDirections.Outbound);

        var token = await GetAuthenticationAsync(appConfig, SCIMDirections.Outbound);

        var httpClient = _httpClientFactory.CreateClient();

        Utils.HttpClientExtensions.SetAuthenticationHeaders(httpClient, appConfig.AuthenticationMethodOutbound, appConfig.AuthenticationDetails, token, SCIMDirections.Outbound);

        var apiUrl = DynamicApiUrlUtil.GetFullUrl(userURIs!.Delete!.ToString(), identifier);

        using (var response = await httpClient.DeleteAsync(apiUrl))
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"HTTP request failed with error: {response.StatusCode} - {response.ReasonPhrase}"
                );
            }
        }
    }
}
