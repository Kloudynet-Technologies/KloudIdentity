//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

/// <summary>
/// Class for creating a new Core2User resource.
/// Implements the ICreateResource interface.
/// </summary>
[Obsolete("This class is obsolete. Use CreateUserV2 instead.")]
public class CreateUser : OperationsBase<Core2EnterpriseUser>, ICreateResource<Core2EnterpriseUser>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IKloudIdentityLogger _logger;

    /// <summary>
    /// Initializes a new instance of the CreateUser class.
    /// </summary>
    /// <param name="configReader">An implementation of IConfigReader for reading configuration settings.</param>
    /// <param name="authContext">An implementation of IAuthContext for handling authentication.</param>
    public CreateUser(
        IAuthContext authContext,
        IHttpClientFactory httpClientFactory,
        IGetFullAppConfigQuery getFullAppConfigQuery,
        IKloudIdentityLogger logger)
        : base(authContext, getFullAppConfigQuery)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Executes the creation of a new user asynchronously.
    /// </summary>
    /// <param name="resource">The user resource to create.</param>
    /// <param name="appId">The ID of the application.</param>
    /// <param name="correlationID">The correlation ID.</param>
    /// <returns>The created user resource.</returns>
    [Obsolete("This method is obsolete. Use CreateUserV2.ExecuteAsync instead.")]
    public async Task<Core2EnterpriseUser> ExecuteAsync(
        Core2EnterpriseUser resource,
        string appId,
        string correlationID
    )
    {
        var appConfig = await GetAppConfigAsync(appId);

        var userAttributes = appConfig.UserAttributeSchemas.Where(x => x.HttpRequestType == HttpRequestTypes.POST).ToList();

        var payload = await MapAndPreparePayloadAsync(userAttributes, resource);

        await CreateUserAsync(appConfig, payload);

        await CreateLogAsync(appConfig, correlationID);

        return resource;
    }

    /// <summary>
    /// Asynchronously creates a new user by sending a request to the user provisioning API.
    /// Authentication is done using the authentication method specified in the application configuration.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="HttpRequestException">
    /// HTTP request failed with error: {response.StatusCode} - {response.ReasonPhrase}
    /// </exception>
    private async Task CreateUserAsync(AppConfig appConfig, JObject payload)
    {
        var userURIs = appConfig.UserURIs.FirstOrDefault();

        var token = await base.GetAuthenticationAsync(appConfig, SCIMDirections.Outbound);

        var httpClient = _httpClientFactory.CreateClient();

        Utils.HttpClientExtensions.SetAuthenticationHeaders(httpClient, appConfig.AuthenticationMethodOutbound, appConfig.AuthenticationDetails, token);

        using (var response = await httpClient.PostAsJsonAsync(
            userURIs?.Post,
            payload
        ))
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Error creating user: {response.StatusCode} - {response.ReasonPhrase}"
                );
            }
        }
    }

    private async Task CreateLogAsync(AppConfig appConfig, string correlationID)
    {
        var logMessage = $"User created to the application #{appConfig.AppName}({appConfig.AppId})";

        var logEntity = new CreateLogEntity(
            appConfig.AppId,
            LogType.Provision.ToString(),
            LogSeverities.Information,
            "User created successfully",
            logMessage,
            correlationID,
            AppConstant.LoggerName,
            DateTime.UtcNow,
            AppConstant.User,
            null,
            null
        );

        await _logger.CreateLogAsync(logEntity);
    }

}

