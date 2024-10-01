using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.Domain.Inbound;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Queries
{
    public class GetInboundAppConfigQuery : IGetInboundAppConfigQuery
    {
        private readonly IRequestClient<IMgtPortalServiceRequestMsg> _requestClient;

        public GetInboundAppConfigQuery(IServiceScopeFactory serviceScopeFactory)
        {
            using var serviceScope = serviceScopeFactory.CreateScope();
            _requestClient = serviceScope.ServiceProvider.GetRequiredService<IRequestClient<IMgtPortalServiceRequestMsg>>();
        }

        public async Task<InboundConfig> GetAsync(string appId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                throw new ArgumentNullException(nameof(appId));
            }

            return await SendMessageAndProcessResponse(appId);
        }

        private async Task<InboundConfig> SendMessageAndProcessResponse(string appId)
        {
            var message = new MgtPortalServiceRequestMsg(
                appId,
                ActionType.GetInboundConfigurations.ToString(),
                Guid.NewGuid().ToString(),
                null
            );

            try
            {
                var response = await _requestClient.GetResponse<IInterserviceResponseMsg>(message);

                return ProcessResponse(response.Message);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(ex.Message);
            }
        }

        private static InboundConfig ProcessResponse(IInterserviceResponseMsg? response)
        {
            if (response == null || response.IsError == true)
            {
                throw new InvalidOperationException($"{response?.ErrorMessage ?? "Unknown error"}", response?.ExceptionDetails);
            }

            var appConfig = JsonConvert.DeserializeObject<InboundConfig>(response.Message);

            return appConfig ?? throw new KeyNotFoundException("Inbound application not found");
        }
    }
}
