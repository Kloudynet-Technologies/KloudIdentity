using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.ExternalEndpoint;
using KN.KloudIdentity.Mapper.Domain.Mapping;

namespace KN.KloudIdentity.Mapper.Domain.Application;

public record AppConfig
{
    public string AppId { get; init; } = string.Empty;
    public string AppName { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public IntegrationMethods? IntegrationMethodInbound { get; init; }
    public IntegrationMethods? IntegrationMethodOutbound { get; init; }
    public List<UserURIs>? UserURIs { get; init; } // Now optional
    public List<GroupURIs>? GroupURIs { get; init; }
    public string? AADClientId { get; init; }
    public AuthenticationMethods AuthenticationMethodInbound { get; init; }
    public AuthenticationMethods AuthenticationMethodOutbound { get; init; }
    public required dynamic AuthenticationDetails { get; set; }
    public ICollection<AttributeSchema>? UserAttributeSchemas { get; set; }
    public ICollection<AttributeSchema>? GroupAttributeSchemas { get; set; }
    public bool? IsExternalAPIEnabled { get; set; }
    public ExternalEndpointInfo? ExternalEndpointInfo { get; set; }
    public dynamic? IntegrationDetails { get; set; }

    public ICollection<Action> Actions { get; set; } = [];

    /// <summary>
    /// Validates that UserURIs is set when IntegrationMethodOutbound is REST.
    /// Throws InvalidOperationException if the requirement is not met.
    /// </summary>
    public void Validate()
    {
        if (IntegrationMethodOutbound != IntegrationMethods.REST && (UserURIs == null || UserURIs.Count == 0))
        {
            throw new InvalidOperationException($"UserURIs must be provided for {IntegrationMethodOutbound} integration method.");
        }

        if (IntegrationMethodOutbound != IntegrationMethods.REST && (UserAttributeSchemas == null || UserAttributeSchemas.Count == 0))
        {
            throw new InvalidOperationException($"UserAttributeSchemas must be provided for {IntegrationMethodOutbound} integration method.");
        }
    }
}
