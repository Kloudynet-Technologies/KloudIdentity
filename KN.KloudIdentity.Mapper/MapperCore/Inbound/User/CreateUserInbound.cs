using System.Text;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Inbound;
using KN.KloudIdentity.Mapper.Domain.Mapping.Inbound;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Inbound.Utils;
using Newtonsoft.Json;
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
            IInboundMapper inboundMapper,
            IGetInboundAppConfigQuery getInboundAppConfigQuery) : base(authContext, inboundMapper, getInboundAppConfigQuery)
        {
            _graphClientUtil = graphClientUtil;
            _listUsersInbound = fetchInboundResources;
        }

        public async Task ExecuteAsync(string appId)
        {
            var inboundConfig = await GetAppConfigAsync(appId);

            var integrationConfig = GetInboundRESTIntegrationConfig(inboundConfig);

            var users = await _listUsersInbound.FetchInboundResourcesAsync(inboundConfig, CorrelationID) ??
                        throw new ApplicationException("No users fetched from the LOB app to be provisioned to IGA. Exiting the process.");

            InboundMappingConfig inboundMappingConfig = new(

                inboundConfig.InboundAttMappingUsersPath,
                inboundConfig.InboundAttributeMappings.ToList()
            );

            var mappedPayload = await MapAndPreparePayloadAsync(inboundMappingConfig, users);

            var graphClient = await _graphClientUtil.GetClientAsync(inboundConfig.TenantId, inboundConfig.ClientId, inboundConfig.ClientSecret);

            var requestContent = new StringContent(mappedPayload.ToString(), Encoding.UTF8, "application/scim+json");

            var response = await graphClient.PostAsync(integrationConfig.ProvisioningEndpoint, requestContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error creating user: {response.StatusCode}, {errorContent}");
            }
        }

        private InboundRESTIntegrationConfig GetInboundRESTIntegrationConfig(InboundConfig config)
        {
            var restConfig = JsonConvert.DeserializeObject<InboundRESTIntegrationConfig>(config.IntegrationDetails.ToString());

            return restConfig!;
        }
    }
}
