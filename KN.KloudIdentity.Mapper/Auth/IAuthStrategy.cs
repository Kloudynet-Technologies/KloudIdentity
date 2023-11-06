using System.Security.Authentication;
using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Config;

namespace KN.KloudIdentity.Mapper;

public interface IAuthStrategy
{
    AuthenticationMethod AuthenticationMethod { get; }

    /// <summary>
    /// Gets the auth token.
    /// </summary>
    Task<string> GetTokenAsync(AuthConfig authConfig);
}
