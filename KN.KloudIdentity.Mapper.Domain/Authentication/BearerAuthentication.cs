namespace KN.KloudIdentity.Mapper.Domain.Authentication;

public record BearerAuthentication
{
    public string Token { get; init; }
    public EncryptedData EncryptedData { get; init; }
    public string? KeyVaultReference { get; init; }
}
