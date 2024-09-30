using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping.Inbound;

namespace KN.KloudIdentity.Mapper.Domain.Inbound;

public record InboundConfig
{
    public string AppId { get; init; } = string.Empty;

    public string AADAppClientId { get; init; } = string.Empty;

    public string AADAppObjectId { get; init; } = string.Empty;

    public IntegrationMethods IntegrationMethodInbound { get; init; }

    public AuthenticationMethods AuthenticationMethodInbound { get; init; }

    public required dynamic AuthenticationDetails { get; set; }

    public string InboundAttMappingUsersPath { get; init; } = string.Empty;

    public IList<InboundAttributeMapping> InboundAttributeMappings { get; init; } = [];
}
