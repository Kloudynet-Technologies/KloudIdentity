using KN.KloudIdentity.Mapper.Auth;
using KN.KloudIdentity.Mapper.Config;

namespace KN.KloudIdentity.Mapper;

public class ApiKeyStrategy : IAuthStrategy
{
    public AuthenticationMethod AuthenticationMethod => AuthenticationMethod.ApiKey;

    public Task<string> GetTokenAsync(AuthConfig authConfig)
    {
        throw new NotImplementedException();
    }
}
