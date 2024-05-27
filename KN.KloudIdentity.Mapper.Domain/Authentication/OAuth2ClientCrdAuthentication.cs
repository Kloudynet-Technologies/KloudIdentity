namespace KN.KloudIdentity.Mapper.Domain.Authentication;

public record OAuth2ClientCrdAuthentication
{
    /// <summary>
    /// ClientId
    /// </summary>
    public string ClientId { get; private set; } = null!;

    /// <summary>
    /// ClientSecret
    /// </summary>
    public string ClientSecret { get; private set; } = null!;

    /// <summary>
    /// Scopes
    /// </summary>
    public IEnumerable<string> Scopes { get; private set; } = null!;

    /// <summary>
    /// Token aquisition url
    /// </summary>
    public string TokenUrl { get; private set; } = null!;

    /// <summary>
    /// Authority
    /// </summary>
    public string? Authority { get; private set; }
}
