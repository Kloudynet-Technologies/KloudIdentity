using KN.KloudIdentity.Mapper.Domain.Authentication;

namespace KN.KloudIdentity.Mapper.Domain.ExternalEndpoint;

public record ExternalEndpointInfo
{
    public Guid Id { get; set; }
    public required string AppId { get; set; }
    public required string EndpointUrl { get; init; }
    public AuthenticationMethods AuthenticationMethod { get; set; }
    public ExternalAPIKeyAuth? APIKeyAuth { get; set; }
    public ExternalBearerAuth? BearerAuth { get; set; }
}
