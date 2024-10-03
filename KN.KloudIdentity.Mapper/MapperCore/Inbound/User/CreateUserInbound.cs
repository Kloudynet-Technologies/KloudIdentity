using System.Text;
using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
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
        private readonly IKloudIdentityLogger _logger;

        public CreateUserInbound(IAuthContext authContext,
            IGraphClientUtil graphClientUtil,
            IFetchInboundResources fetchInboundResources,
            IInboundMapper inboundMapper,
            IGetInboundAppConfigQuery getInboundAppConfigQuery,
            IKloudIdentityLogger logger) : base(authContext, inboundMapper, getInboundAppConfigQuery, logger)
        {
            _graphClientUtil = graphClientUtil;
            _listUsersInbound = fetchInboundResources;
            _logger = logger;
        }

        public async Task ExecuteAsync(string appId)
        {
            _ = CreateLogAsync(appId, LogSeverities.Information, "CreateUserInbound started", CorrelationID);

            var inboundConfig = await GetAppConfigAsync(appId);

            var integrationConfig = GetInboundRESTIntegrationConfig(inboundConfig);

            var users = await _listUsersInbound.FetchInboundResourcesAsync(inboundConfig, CorrelationID) ??
                        throw new ApplicationException("No users fetched from the LOB app to be provisioned to IGA. Exiting the process.");

            InboundMappingConfig inboundMappingConfig = new(

                inboundConfig.InboundAttMappingUsersPath,
                inboundConfig.InboundAttributeMappings.ToList()
            );

            var mappedPayload = await MapAndPreparePayloadAsync(inboundMappingConfig, users, inboundConfig.AppId);

            var graphClient = await _graphClientUtil.GetClientAsync(inboundConfig.TenantId, inboundConfig.ClientId, inboundConfig.ClientSecret);

            var requestContent = new StringContent(mappedPayload.ToString(), Encoding.UTF8, "application/scim+json");

            var response = await graphClient.PostAsync(integrationConfig.ProvisioningEndpoint, requestContent);

            _ = CreateLogAsync(appId, LogSeverities.Information, "Inbound user provisioning payload posted", CorrelationID);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _ = CreateLogAsync(appId, LogSeverities.Error, $"Error creating user: {response.StatusCode}, {errorContent}", CorrelationID);

                throw new ApplicationException($"Error creating user: {response.StatusCode}, {errorContent}");
            }
        }

        private InboundRESTIntegrationConfig GetInboundRESTIntegrationConfig(InboundConfig config)
        {
            var restConfig = JsonConvert.DeserializeObject<InboundRESTIntegrationConfig>(config.IntegrationDetails.ToString());

            return restConfig!;
        }

        private async Task CreateLogAsync(string appId, LogSeverities severity, string message, string correlationId)
        {
            await _logger.CreateLogAsync(new CreateLogEntity
            (
                appId,
                "Inbound",
                severity,
                "CreateUserInbound",
                message,
                correlationId,
                "KN.KloudIdentity",
                DateTime.UtcNow,
                "SYSTEM",
                null,
                null
            ));
        }
    }
}
