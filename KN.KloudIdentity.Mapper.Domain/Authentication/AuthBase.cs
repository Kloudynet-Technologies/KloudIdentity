namespace KN.KloudIdentity.Mapper.Domain.Authentication;

public record AuthBase
{
    /// <summary>
    /// Application Id
    /// </summary>
    public string AppId { get; init; }

    /// <summary>
    /// Authentication method
    /// </summary>
    public AuthenticationMethods AuthenticationMethod { get; init; }
}
