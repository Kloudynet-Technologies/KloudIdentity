using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.SCIM;
using System.Net.Http.Headers;
using System.Text;

namespace KN.KloudIdentity.Mapper.MapperCore.User;

/// <summary>
/// Class for creating a new Core2User resource.
/// Implements the ICreateResource interface.
/// </summary>
public class CreateUser : OperationsBase<Core2EnterpriseUser>, ICreateResource<Core2EnterpriseUser>
{
    private MapperConfig _appConfig;

    /// <summary>
    /// Initializes a new instance of the CreateUser class.
    /// </summary>
    /// <param name="configReader">An implementation of IConfigReader for reading configuration settings.</param>
    /// <param name="authContext">An implementation of IAuthContext for handling authentication.</param>
    public CreateUser(IConfigReader configReader, IAuthContext authContext)
        : base(configReader, authContext) { }

    /// <summary>
    /// Executes the creation of a new user asynchronously.
    /// </summary>
    /// <param name="resource">The user resource to create.</param>
    /// <param name="appId">The ID of the application.</param>
    /// <param name="correlationID">The correlation ID.</param>
    /// <returns>The created user resource.</returns>
    public async Task<Core2EnterpriseUser> ExecuteAsync(
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
        var token = await GetAuthenticationAsync();
        var authConfig = _appConfig.AuthConfig;

        using (var httpClient = new HttpClient())
        {
            if (authConfig.AuthenticationMethod == AuthenticationMethod.ApiKey)
            {
                if (string.IsNullOrWhiteSpace(authConfig.ApiKeyHeader))
                {
                    throw new ArgumentNullException(
                        nameof(authConfig.ApiKeyHeader),
                        "ApiKeyHeaderName cannot be null or empty when AuthenticationMethod is ApiKey"
                    );
                }

                httpClient.DefaultRequestHeaders.Add(authConfig.ApiKeyHeader, token);
            }
            else
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    token
                );
            }

            var response = await httpClient.PostAsJsonAsync(
                _appConfig.UserProvisioningApiUrl,
                Payload
            );

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Error creating user: {response.StatusCode} - {response.ReasonPhrase}"
                );
            }
        }
    }
}

