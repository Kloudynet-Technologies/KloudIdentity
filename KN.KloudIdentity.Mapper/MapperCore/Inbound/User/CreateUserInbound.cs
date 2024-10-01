using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.Graph.Models;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound.User
{
    public class CreateUserInbound : OperationsBaseInbound, ICreateResourceInbound
    {
        private readonly IGraphClientUtil _graphClientUtil;
        private readonly IGetApplicationSettingQuery _getAppSettingQuery;

        public CreateUserInbound(IAuthContext authContext,
            IGraphClientUtil graphClientUtil,
            IGetApplicationSettingQuery getApplicationSettingQuery,
            IGetInboundAppConfigQuery getInboundAppConfigQuery) : base(authContext, getInboundAppConfigQuery)
        {
            _graphClientUtil = graphClientUtil ?? throw new ArgumentNullException(nameof(graphClientUtil));
            _getAppSettingQuery = getApplicationSettingQuery ?? throw new ArgumentNullException(nameof(getApplicationSettingQuery));
        }

        public async Task ExecuteAsync(IList<JObject> resources, string appId, string correlationId)
        {
            var appConfig = await GetAppConfigAsync(appId);

            await CreateUserAsync(appId);
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
