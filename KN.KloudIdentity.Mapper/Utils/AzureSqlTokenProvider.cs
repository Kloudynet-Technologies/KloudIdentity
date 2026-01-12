using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace KN.KloudIdentity.Mapper.Utils;

public static class AzureSqlTokenProvider
{
    private static readonly TokenRequestContext TokenContext =
        new(new[] { "https://database.windows.net/.default" });

    private static ClientSecretCredential? _credential;

    public static async Task<string> GetTokenAsync(IConfiguration config)
    {
        _credential ??= new ClientSecretCredential(
            config["TENANT_ID"],
            config["SA_CLIENT_ID"],
            config["SA_CLIENT_SECRET"]);

        var token = await _credential.GetTokenAsync(TokenContext);
        return token.Token;
    }
}