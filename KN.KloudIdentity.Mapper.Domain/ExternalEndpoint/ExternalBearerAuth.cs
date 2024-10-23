namespace KN.KloudIdentity.Mapper.Domain.ExternalEndpoint;

public record ExternalBearerAuth
{
    public Guid? Id { get; init; }
    public required string BearerToken { get; init; }
}
