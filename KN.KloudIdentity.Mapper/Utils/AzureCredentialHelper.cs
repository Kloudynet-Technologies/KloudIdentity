using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace KN.KloudIdentity.Mapper.Utils;

public static class AzureCredentialHelper
{

    private const string TenantIdKey = "AZURE_TENANT_ID";
    private const string ClientIdKey = "AZURE_CLIENT_ID";
    private const string ClientSecretKey = "AZURE_CLIENT_SECRET";

    public static ClientSecretCredential CreateClientSecretCredential(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var tenantId = GetRequiredValue(configuration, TenantIdKey);
        var clientId = GetRequiredValue(configuration, ClientIdKey);
        var clientSecret = GetRequiredValue(configuration, ClientSecretKey);

        return new ClientSecretCredential(tenantId, clientId, clientSecret);
    }

    private static string GetRequiredValue(IConfiguration configuration, string key)
    {
        var value = configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentNullException(
                key,
                $@"Configuration error: Required environment variable '{key}' is missing or empty. " +
                $@"Please set '{key}' in your environment variables, Kubernetes Secret, or app settings configuration.");
        }

        return value;
    }
}