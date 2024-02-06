namespace KN.KloudIdentity.Mapper.Domain.Authentication;

public record BearerAuthentication
{
    public string Token { get; init; }
}
