using Azure.Security.KeyVault.Secrets;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Shared;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Queries;

public class AzureSecretManager : ISecretManager
{
    private readonly SecretClient _secretClient;

    public AzureSecretManager(IOptionsSnapshot<AppSettings> appSettings, IConfiguration configuration)
    {
        var keyVaultUrl = appSettings.Value.AzureKeyVault?.Url;
        if (string.IsNullOrWhiteSpace(keyVaultUrl))
        {
            throw new ArgumentException("Azure Key Vault URL is not configured in KI:AzureKeyVault:Url.");
        }

        _secretClient = new SecretClient(new Uri(keyVaultUrl), AzureCredentialHelper.CreateClientSecretCredential(configuration));
    }
    
    public async Task<string> GetSecretAsync(string secretKeyVaultRef)
    {
        if (string.IsNullOrWhiteSpace(secretKeyVaultRef))
        {
            throw new ArgumentException("Secret key vault reference is not provided.");
        }

        KeyVaultSecret secret = await _secretClient.GetSecretAsync(secretKeyVaultRef);
        return secret.Value;
    }
}