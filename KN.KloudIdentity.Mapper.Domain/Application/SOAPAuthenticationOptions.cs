using KN.KloudIdentity.Mapper.Domain.Authentication;

namespace KN.KloudIdentity.Mapper.Domain.Application;

public record SOAPAuthenticationOptions
{
    public BasicOrNtlmSoapAuthOptions? Transport { get; init; }
    public WsSecuritySoapAuthOptions? WsSecurity { get; init; }
    public SoapTokenPlacementOptions? TokenPlacement { get; init; }
}

public record BasicOrNtlmSoapAuthOptions
{
    public bool Enabled { get; init; }
    public bool UseNtlm { get; init; }
    public bool UseDefaultCredentials { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? Domain { get; init; }
    public string? KeyVaultReference { get; init; }
    public EncryptedData? EncryptedData { get; init; }
}

public record WsSecuritySoapAuthOptions
{
    public bool Enabled { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public bool IncludeTimestamp { get; init; }
    public string? KeyVaultReference { get; init; }
    public EncryptedData? EncryptedData { get; init; }
}

public record SoapTokenPlacementOptions
{
    public bool Enabled { get; init; }
    public bool UseAuthorizationHeader { get; init; }
    public Dictionary<string, string>? CustomHttpHeaders { get; init; }
    public string? SoapHeaderTemplate { get; init; }
    public string TokenPlaceholder { get; init; } = "{{token}}";
}
