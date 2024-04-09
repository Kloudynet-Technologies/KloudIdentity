using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using Microsoft.Graph.Models;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound.User
{
    public class CreateUserInbound : OperationsBaseInbound, ICreateResourceInbound<JObject>
    {
        private IGraphClientUtil _graphClientUtil;
        private IGetApplicationSettingQuery _getAppSettingQuery;
        public CreateUserInbound(IAuthContext authContext,
            IGetFullAppConfigQuery getFullAppConfigQuery,
            IGraphClientUtil graphClientUtil,
            IGetApplicationSettingQuery getApplicationSettingQuery) : base(authContext, getFullAppConfigQuery)
        {
            _graphClientUtil = graphClientUtil;
            _getAppSettingQuery = getApplicationSettingQuery;
        }

        public async Task ExecuteAsync(IList<JObject> resources, string appId, string correlationId)
        {
            var appConfig = await GetAppConfigAsync(appId, correlationId);

            var userAttributes = appConfig.UserAttributeSchemas.Where(x => x.HttpRequestType == HttpRequestTypes.POST &&
                                  x.SCIMDirection == SCIMDirections.Inbound).ToList();

            //  var payload = MapAndPreparePayloadAsync(appConfig.UserAttributeSchemas, resources);

            CreateUserAsync(appId);
        }

        private async Task CreateUserAsync(string appId)
        {
            var setting = await _getAppSettingQuery.GetAsync(appId);

            var graphClient = _graphClientUtil.GetClient(setting.TenantId, setting.ClientId, setting.ClientSecret);

            var requestBody = new Microsoft.Graph.Models.User
            {
                AccountEnabled = true,
                DisplayName = "Testing Adele Vance",
                MailNickname = "AdeleV",
                UserPrincipalName = "test@test.com",
                PasswordProfile = new PasswordProfile
                {
                    ForceChangePasswordNextSignIn = true,
                    Password = "xWwvJ]6NMw+bWH-d",
                },
            };
            var result = await graphClient.Users.PostAsync(requestBody);
        }
    }
}
