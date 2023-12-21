using System.Web.Http;
using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.MapperCore.User;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.SCIM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.MapperOverride;

public class GetUser_Zoho : GetUser
{
    private MapperConfig _appConfig;
    private readonly UserIdMapperUtil _userIdMapperUtil;
    private readonly IHttpClientFactory _httpClientFactory;

    public GetUser_Zoho(IConfigReader configReader, IAuthContext authContext, IHttpClientFactory httpClientFactory, IConfiguration configuration, UserIdMapperUtil userIdMapperUtil) : base(configReader, authContext, httpClientFactory, configuration, userIdMapperUtil)
    {
        _userIdMapperUtil = userIdMapperUtil;
        _httpClientFactory = httpClientFactory;
    }

    public override Task<string> GetAuthenticationAsync(AuthConfig config)
    {
        return Task.FromResult($"{config.Token}");
    }

    public override async Task<Core2EnterpriseUser> GetAsync(string identifier, string appId, string correlationID)
    {
        AppId = appId;
        CorrelationID = correlationID;

        _appConfig = await GetAppConfigAsync();

        if (_appConfig.GETAPIForUsers != null && _appConfig.GETAPIForUsers != string.Empty)
        {
            var token = await GetAuthenticationAsync(_appConfig.AuthConfig);

            // @TODO: Get the created user id from the database based on app config setting.
            var userId = _userIdMapperUtil.GetCreatedUserId(identifier, appId);

            var client = _httpClientFactory.CreateClient();
            client.SetAuthenticationHeaders(_appConfig.AuthConfig, token);
            var response = await client.GetAsync(DynamicApiUrlUtil.GetFullUrl(_appConfig.GETAPIForUsers, userId));

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var user = JsonConvert.DeserializeObject<JObject>(content);

                var core2EntUsr = new Core2EnterpriseUser
                {
                    Identifier = identifier,
                    UserName = user.SelectToken("users")[0]?.SelectToken("email")?.ToString()
                };

                return core2EntUsr;
            }
            else
            {
                throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
            }
        }
        else
        {
            throw new ApplicationException("GET API for users is not configured.");
        }
    }
}
