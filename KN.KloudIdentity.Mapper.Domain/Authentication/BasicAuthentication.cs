namespace KN.KloudIdentity.Mapper.Domain.Authentication;

public record BasicAuthentication
{
    /// <summary>
    /// Username
    /// </summary>
    public string Username { get; init; }

    /// <summary>
    /// Password
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Authorization header name [Optional]
    /// </summary>
    public string? AuthHeaderName { get; init; }
    
    public EncryptedData EncryptedData { get; init; }
}
