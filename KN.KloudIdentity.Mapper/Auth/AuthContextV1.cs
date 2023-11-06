using System.Security.Authentication;
using KN.KloudIdentity.Mapper.Config;

namespace KN.KloudIdentity.Mapper;

public class AuthContextV1 : IAuthContext
{
    IAuthStrategy? _authStrategy;
    IEnumerable<IAuthStrategy> _authStrategies;

    public AuthContextV1(IEnumerable<IAuthStrategy> authStrategies)
    {
        _authStrategies = authStrategies;
    }

    public async Task<string> GetTokenAsync(AuthConfig authConfig)
    {
        _authStrategy = _authStrategies.FirstOrDefault(x => x.AuthenticationMethod == authConfig.AuthenticationMethod);

        if (_authStrategy == null)
        {
            throw new AuthenticationException($"Authentication method {authConfig.AuthenticationMethod} is not supported.");
        }

        return await _authStrategy.GetTokenAsync(authConfig);
    }
}
