namespace KN.KloudIdentity.Mapper.Domain.Authentication;

public record APIKeyAuthentication
{
    public string APIKey { get; init; }

    public string AuthHeaderName { get; init; }

    public DateTime? ExpirationDate { get; init; }
}
