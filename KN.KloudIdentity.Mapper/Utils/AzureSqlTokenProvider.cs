using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace KN.KloudIdentity.Mapper.Utils;

public static class AzureSqlTokenProvider
{
    private static readonly TokenRequestContext TokenContext =
        new(["https://database.windows.net/.default"]);

    private static ClientSecretCredential? _credential;

    public static async Task<string> GetTokenAsync(IConfiguration config)
    {
        _credential ??= new ClientSecretCredential(
            config["AZURE_TENANT_ID"],
            config["AZURE_CLIENT_ID"],
            config["AZURE_CLIENT_SECRET"]);

        var token = await _credential.GetTokenAsync(TokenContext);
        return token.Token;
    }
}