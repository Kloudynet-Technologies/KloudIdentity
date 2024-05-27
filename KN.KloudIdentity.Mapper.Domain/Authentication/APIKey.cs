namespace KN.KloudIdentity.Mapper.Domain.Authentication;

public record APIKey : AuthBase
{
    /// <summary>
    /// API Key
    /// </summary>
    public string Key { get; init; }

}
