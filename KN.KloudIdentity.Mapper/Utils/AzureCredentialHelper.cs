using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace KN.KloudIdentity.Mapper.Utils;

public static class AzureCredentialHelper
{
    public static ClientSecretCredential CreateClientSecretCredential(IConfiguration configuration)
    {
        var tenantId = configuration["AZURE_TENANT_ID"];
        var clientId = configuration["AZURE_CLIENT_ID"];
        var clientSecret = configuration["AZURE_CLIENT_SECRET"];
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new InvalidOperationException("Configuration value 'AZURE_TENANT_ID' is missing or empty. It must be set to create a ClientSecretCredential for Azure App Configuration / Key Vault access.");
        }
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("Configuration value 'AZURE_CLIENT_ID' is missing or empty. It must be set to create a ClientSecretCredential for Azure App Configuration / Key Vault access.");
        }
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("Configuration value 'AZURE_CLIENT_SECRET' is missing or empty. It must be set to create a ClientSecretCredential for Azure App Configuration / Key Vault access.");
        }

        return new ClientSecretCredential(tenantId, clientId, clientSecret);
    }
}