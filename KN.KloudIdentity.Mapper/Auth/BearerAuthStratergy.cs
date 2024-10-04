using KN.KloudIdentity.Mapper.Domain.Authentication;
using Newtonsoft.Json;

namespace KN.KloudIdentity.Mapper;

public class BearerAuthStratergy : IAuthStrategy
{
    public AuthenticationMethods AuthenticationMethod => AuthenticationMethods.Bearer;

    public Task<string> GetTokenAsync(dynamic authConfig)
    {
        var bearerAuth = JsonConvert.DeserializeObject<BearerAuthentication>(authConfig.ToString());

        if (authConfig == null || bearerAuth == null)
        {
            throw new ArgumentNullException(nameof(authConfig));
        }

        if (string.IsNullOrWhiteSpace(bearerAuth?.Token))
        {
            throw new ArgumentNullException(nameof(authConfig.Token));
        }

        return Task.FromResult(bearerAuth?.Token);
    }
}
