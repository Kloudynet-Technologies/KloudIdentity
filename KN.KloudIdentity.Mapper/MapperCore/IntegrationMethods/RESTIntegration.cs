using System;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore;

/// <summary>
/// Integration logic implementation for REST.
/// </summary>
public class RESTIntegration : IIntegrationBase
{
    private readonly IAuthContext _authContext;
    private readonly IHttpClientFactory _httpClientFactory;

    public IntegrationMethods IntegrationMethod { get; init; }

    public RESTIntegration(IAuthContext authContext,
                            IHttpClientFactory httpClientFactory)
    {
        IntegrationMethod = IntegrationMethods.REST;

        _authContext = authContext;
        _httpClientFactory = httpClientFactory;
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
}
