using System.Security.Authentication;
using System.Text;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common.Encryption;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace KN.KloudIdentity.Mapper.MapperCore;

public class ASNBKioskIntegration : RESTIntegrationV4
{
    private readonly ISecretManager _secretManager;
    private readonly AppSettings _appSettings;
    private const string TokenAuthPath = "/TokenAuth/Authenticate";

    public ASNBKioskIntegration(
        IAuthContext authContext,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IKloudIdentityLogger logger,
        IOptions<AppSettings> appSettings,
        ISecretManager secretManager)
        : base(authContext, httpClientFactory, configuration, logger, appSettings)
    {
        _secretManager = secretManager;
        _appSettings = appSettings.Value;
        IntegrationMethod = IntegrationMethods.REST;
    }

    public override async Task<dynamic> GetAuthenticationAsync(
        AppConfig config,
        SCIMDirections direction = SCIMDirections.Outbound,
        CancellationToken cancellationToken = default,
        params dynamic[] args)
    {
        //Locate the Basic auth step and extract credentials
        var basicStep = config.AuthenticationFlow?.Steps
            .FirstOrDefault(s => s.AuthenticationMethod == AuthenticationMethods.Basic)
            ?? throw new AuthenticationException(
                $"No Basic authentication step found in flow for app {config.AppId}.");

        var auth = JsonConvert.DeserializeObject<BasicAuthentication>(
            basicStep.AuthenticationDetails.ToString())
            ?? throw new AuthenticationException(
                $"Failed to deserialize BasicAuthentication details for app {config.AppId}.");

        var encryptedPassword = await _secretManager.GetSecretAsync(auth.KeyVaultReference!);
        var password = EncryptionHelper.Decrypt(
            encryptedPassword,
            _appSettings.EncryptionKey,
            auth.EncryptedData!.IV);

        // Derive ASNB auth URL from any configured User action step endpoint.
        // Prefer GET (most stable URL shape), fall back to any other User action.
        // This prevents failure when the app is configured with only CREATE actions.
        var getEndpoint = config.Actions?
            .Where(a => a.ActionTarget == ActionTargets.USER)
            .OrderBy(a => a.ActionName == ActionNames.GET ? 0 : 1)
            .SelectMany(a => a.ActionSteps ?? Enumerable.Empty<ActionStep>())
            .Select(s => s.EndPoint)
            .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));

        if (string.IsNullOrWhiteSpace(getEndpoint))
            throw new InvalidOperationException(
                $"No User action step endpoint configured for app {config.AppId}. Cannot derive ASNB auth URL.");

        var authUrl = BuildAuthUrl(getEndpoint);

        // POST credentials to ASNB auth endpoint
        using var authClient = _httpClientFactory.CreateClient();

        var requestBody = JsonConvert.SerializeObject(new
        {
            userNameOrEmailAddress = auth.Username,
            password
        });

        using var httpContent = new StringContent(requestBody, Encoding.UTF8, "application/json");
        var response = await authClient.PostAsync(authUrl, httpContent, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new AuthenticationException(
                $"ASNB Kiosk auth HTTP call failed for app {config.AppId}. " +
                $"StatusCode: {response.StatusCode}, Body: {responseBody}");

        // Parse and validate the token
        var responseJson = JObject.Parse(responseBody);

        if (responseJson["success"]?.Value<bool>() != true)
        {
            var error = responseJson["error"]?.ToString() ?? "Unknown error";
            throw new AuthenticationException(
                $"ASNB Kiosk authentication failed for app {config.AppId}: {error}");
        }

        var accessToken = responseJson["result"]?["accessToken"]?.ToString();
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new AuthenticationException(
                $"ASNB Kiosk auth response missing accessToken for app {config.AppId}.");

        Log.Information("ASNB Kiosk auth token acquired successfully for app {AppId}", config.AppId);

        return new Dictionary<int, string> { [basicStep.StepOrder] = accessToken };
    }

    private static string BuildAuthUrl(string getEndpoint)
    {
        var uri = new Uri(getEndpoint);
        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        var apiIndex = Array.FindIndex(
            segments,
            s => s.Equals("api", StringComparison.OrdinalIgnoreCase));

        if (apiIndex < 0)
            throw new InvalidOperationException(
                $"Cannot derive ASNB auth URL: '/api' segment not found in endpoint '{getEndpoint}'.");

        var basePath = string.Join("/", segments.Take(apiIndex + 1));
        return $"{uri.Scheme}://{uri.Host}/{basePath}{TokenAuthPath}";
    }
}
