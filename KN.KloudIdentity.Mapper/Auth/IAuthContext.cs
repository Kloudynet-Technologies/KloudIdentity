using KN.KloudIdentity.Mapper.Config;

namespace KN.KloudIdentity.Mapper;

/// <summary>
/// Authentication methods for 3rd party API calls.
/// </summary>
public interface IAuthContext
{
    /// <summary>
    /// Gets the auth token.
    /// </summary>
    Task<string> GetTokenAsync(AuthConfig authConfig);
}
