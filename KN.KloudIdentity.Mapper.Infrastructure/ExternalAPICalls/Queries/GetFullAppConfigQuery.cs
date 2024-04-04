using System.Net.Http.Headers;
using System.Text.Json;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.Messaging;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Queries;

public class GetFullAppConfigQuery : IGetFullAppConfigQuery
{
    private readonly MessageBroker _rabbitMQPublisher;
    public GetFullAppConfigQuery(MessageBroker rabbitMQPublisher)
    {
        _rabbitMQPublisher = rabbitMQPublisher;
    }

    public async Task<AppConfig?> GetAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ArgumentNullException(nameof(appId));
        }

        var correlationId = Guid.NewGuid().ToString();
        var intSvcRequest = new InterserviceMessage(appId, correlationId);

        _rabbitMQPublisher.Publish(intSvcRequest, GlobalConstants.MGTPORTAL_IN);

        // Consume the response from the message broker
        AppConfig appConfig = null;
        void HandleResponse(InterserviceMessage? response)
        {
            if (response != null)
            {
                if (!response.IsError)
                {
                    appConfig = JsonSerializer.Deserialize<AppConfig>(response.Message, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
        }

        var consumer = Task.Run(async () =>
        {
            await _rabbitMQPublisher.Consume(GlobalConstants.MGTPORTAL_OUT, correlationId, HandleResponse, cancellationToken);
            while (appConfig == null)
            {
                await Task.Delay(50);
            }
        }, cancellationToken);

        await consumer;

        return appConfig;
    }
}
