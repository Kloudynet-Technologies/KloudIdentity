namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;

public interface ISecretManager
{
    Task<string> GetSecretAsync(string secretKeyVaultRef);
}