//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Identity.Client;
using Microsoft.SCIM;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

/// <summary>
/// Class for creating a new Core2User resource.
/// Implements the ICreateResource interface.
/// </summary>
public class CreateUser : OperationsBase<Core2EnterpriseUser>, ICreateResource<Core2EnterpriseUser>
{
    private MapperConfig _appConfig;
    private readonly IHttpClientFactory _httpClientFactory;

    private readonly UserIdMapperUtil _userIdMapperUtil;

    /// <summary>
    /// Initializes a new instance of the CreateUser class.
    /// </summary>
    /// <param name="configReader">An implementation of IConfigReader for reading configuration settings.</param>
    /// <param name="authContext">An implementation of IAuthContext for handling authentication.</param>
    public CreateUser(IConfigReader configReader, IAuthContext authContext, IHttpClientFactory httpClientFactory, UserIdMapperUtil userIdMapperUtil)
        : base(configReader, authContext)
    {
        _httpClientFactory = httpClientFactory;
        _userIdMapperUtil = userIdMapperUtil;
    }

    /// <summary>
    /// Executes the creation of a new user asynchronously.
    /// </summary>
    /// <param name="resource">The user resource to create.</param>
    /// <param name="appId">The ID of the application.</param>
    /// <param name="correlationID">The correlation ID.</param>
    /// <returns>The created user resource.</returns>
    public virtual async Task<Core2EnterpriseUser> ExecuteAsync(
        Core2EnterpriseUser resource,
        string appId,
        string correlationID
    )
    {
        AppId = appId;
        Resource = resource;
        CorrelationID = correlationID;

        _appConfig = await GetAppConfigAsync();

        await MapAndPreparePayloadAsync();

        await CreateUserAsync();

        return resource;
    }

    /// <summary>
    /// Map and prepare the payload to be sent to the API asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public override async Task MapAndPreparePayloadAsync()
    {
        Payload = JSONParserUtil<Resource>.Parse(_appConfig.UserSchema, Resource);
    }

    /// <summary>
    /// Asynchronously creates a new user by sending a request to the user provisioning API.
    /// Authentication is done using the authentication method specified in the application configuration.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="HttpRequestException">
    /// HTTP request failed with error: {response.StatusCode} - {response.ReasonPhrase}
    /// </exception>
    private async Task CreateUserAsync()
    {
        var authConfig = _appConfig.AuthConfig;

        var token = await GetAuthenticationAsync(authConfig);

        var httpClient = _httpClientFactory.CreateClient();

        httpClient.SetAuthenticationHeaders(authConfig, token);

        using (var response = await httpClient.PostAsJsonAsync(
            _appConfig.UserProvisioningApiUrl,
            Payload
        ))
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Error creating user: {response.StatusCode} - {response.ReasonPhrase}"
                );
            }

            // @TODO: Create user ID mapper entry based on app config setting.
            var createdUser = await response.Content.ReadAsAsync<JObject>();
            var createdUserId = createdUser["users"][0]["details"]["id"].ToString();
        }
    }
}

