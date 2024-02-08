using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;

namespace KN.KloudIdentity.Mapper.Domain.Application;

public record AppConfig
{
    public string AppId { get; init; } = string.Empty;
    public string AppName { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public IntegrationMethods IntegrationMethod { get; init; }
    public required UserURIs UserURIs { get; init; }
    public GroupURIs? GroupURIs { get; init; }
    public string? AADClientId { get; init; }
    public AuthenticationMethods AuthenticationMethod { get; init; }
    public required dynamic AuthenticationDetails { get; set; }
    public required IEnumerable<AttributeSchema> UserAttributeSchemas { get; set; }
    public IEnumerable<AttributeSchema>? GroupAttributeSchemas { get; set; }
}
