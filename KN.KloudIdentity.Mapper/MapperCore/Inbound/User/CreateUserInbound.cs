using System.Text;
using KN.KloudIdentity.Mapper.Domain.Inbound;
using KN.KloudIdentity.Mapper.Domain.Mapping.Inbound;
using KN.KloudIdentity.Mapper.MapperCore.Inbound.Utils;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound.User
{
    public class CreateUserInbound : OperationsBaseInbound, ICreateResourceInbound
    {
        private IGraphClientUtil _graphClientUtil;
        private IFetchInboundResources _listUsersInbound;

        public CreateUserInbound(IAuthContext authContext,
            IGraphClientUtil graphClientUtil,
            IFetchInboundResources fetchInboundResources,
            IInboundMapper inboundMapper) : base(authContext, inboundMapper)
        {
            _graphClientUtil = graphClientUtil;
            _listUsersInbound = fetchInboundResources;
        }

        public async Task ExecuteAsync(string appId)
        {
            var inboundConfig = await GetAppConfigAsync(appId);
            var users = await _listUsersInbound.FetchInboundResourcesAsync(inboundConfig, CorrelationID) ??
                        throw new ApplicationException("No users fetched from the LOB app to be provisioned to IGA. Exiting the process.");

            InboundMappingConfig inboundMappingConfig = new(

                inboundConfig.InboundAttMappingUsersPath,
                inboundConfig.InboundAttributeMappings.ToList()
            );

            var mappedPayload = await MapAndPreparePayloadAsync(inboundMappingConfig, users);

            var graphClient = await _graphClientUtil.GetClientAsync(inboundConfig.TenantId, inboundConfig.ClientId, inboundConfig.ClientSecret);

            var requestContent = new StringContent(mappedPayload.ToString(), Encoding.UTF8, "application/json");

            var response = await graphClient.PostAsync(inboundConfig.InboundProvisioningUrl, requestContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error creating user: {response.StatusCode}, {errorContent}");
            }
        }
    }
}
