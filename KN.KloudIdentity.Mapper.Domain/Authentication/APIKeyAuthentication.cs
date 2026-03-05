namespace KN.KloudIdentity.Mapper.Domain.Authentication;

public record APIKeyAuthentication
{
    public string APIKey { get; set; }

    public string AuthHeaderName { get; init; }

    public DateTime? ExpirationDate { get; init; }
    public EncryptedData EncryptedData { get; init; }
}
