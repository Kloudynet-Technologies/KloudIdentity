namespace KN.KloudIdentity.Mapper.Domain;

public record InboundRESTIntegrationConfig
{
    public Guid Id { get; init; }
    public required string AppId { get; init; }
    public required string UsersEndpoint { get; init; }
    public required string ProvisioningEndpoint { get; init; }
    public required string JoiningDateProperty { get; init; }
}
