using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Config;

namespace KN.KloudIdentity.Mapper;

public class BasicAuthStrategy : IAuthStrategy
{
    public AuthenticationMethod AuthenticationMethod => AuthenticationMethod.Basic;

    public Task<string> GetTokenAsync(AuthConfig authConfig)
    {
        throw new NotImplementedException();
    }
}
