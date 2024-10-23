namespace KN.KloudIdentity.Mapper.Domain.ExternalEndpoint;

public record ExternalAPIKeyAuth
{
    public Guid? Id { get; init; }
    public required string APIKey { get; init; }
    public required string AuthHeaderName { get; init; }
}
