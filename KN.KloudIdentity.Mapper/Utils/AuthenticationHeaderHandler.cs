using KN.KloudIdentity.Mapper.Domain.Authentication;
using Newtonsoft.Json;
using System.Net.Http.Headers;

namespace KN.KloudIdentity.Mapper.Utils;

public class AuthenticationHeaderHandler
{
    public virtual void ApplyHeader(HttpClient httpClient, AuthenticationMethods method, dynamic authConfig, string token)
    {
        switch (method)
        {
            case AuthenticationMethods.None:
                return;

            case AuthenticationMethods.APIKey:
                AddApiKeyHeader(httpClient, authConfig, token);
                break;

            case AuthenticationMethods.OAuth2:
            case AuthenticationMethods.OAuth2ClientCrd:
                AddOAuth2Header(httpClient, authConfig, token);
                break;

            case AuthenticationMethods.Basic:
                AddBasicHeader(httpClient, authConfig, token);
                break;

            case AuthenticationMethods.Bearer:
                AddBearerHeader(httpClient, token);
                break;

            default:
                AddCustomHeader(httpClient, token);
                break;
        }
    }

    protected virtual void AddApiKeyHeader(HttpClient httpClient, dynamic authConfig, string token)
    {
        if (authConfig?.AuthHeaderName == null)
            throw new ArgumentNullException(
                nameof(authConfig.AuthHeaderName),
                "AuthHeaderName cannot be null or empty when AuthenticationMethod is APIKey."
            );

        httpClient.DefaultRequestHeaders.Add(authConfig.AuthHeaderName.ToString(), token);
    }

    protected virtual void AddOAuth2Header(HttpClient httpClient, dynamic authConfig, string token)
    {
        var auth = JsonConvert.DeserializeObject<OAuth2Authentication>(authConfig.ToString());
        var tokenPrefix = string.IsNullOrWhiteSpace(auth.TokenPrefix) ? "Bearer" : auth.TokenPrefix;

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(tokenPrefix, token);
    }

    protected virtual void AddBasicHeader(HttpClient httpClient, dynamic authConfig, string token)
    {
        var auth = JsonConvert.DeserializeObject<BasicAuthentication>(authConfig.ToString());
        var authHeaderName = string.IsNullOrWhiteSpace(auth.AuthHeaderName) ? "Basic" : auth.AuthHeaderName;

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(authHeaderName, token);
    }

    protected virtual void AddBearerHeader(HttpClient httpClient, string token)
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    protected virtual void AddCustomHeader(HttpClient httpClient, string token)
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(token);
    }
}
