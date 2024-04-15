using System.Net.Http.Headers;
using System.Text.Json;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Queries;

public class GetFullAppConfigQuery : IGetFullAppConfigQuery
{
    private readonly MessageBroker _rabbitMQPublisher;
    private readonly IModel _channel;
    public GetFullAppConfigQuery(MessageBroker rabbitMQPublisher, RabbitMQUtil rabbitMQUtil)
    {
        _rabbitMQPublisher = rabbitMQPublisher;
        _channel = rabbitMQUtil.GetChannel();
    }

    public async Task<AppConfig?> GetAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ArgumentNullException(nameof(appId));
        }

        var correlationId = Guid.NewGuid().ToString();
        var intSvcRequest = new InterserviceMessage(appId, correlationId);


        // Consume the response from the message broker
        AppConfig appConfig = null;
        void HandleResponse(InterserviceMessage? response)
        {
            if (response != null)
            {
                if (!response.IsError)
                {
                    appConfig = JsonSerializer.Deserialize<AppConfig>(response.Message, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    _rabbitMQPublisher.Close();
                }
            }
        }

        var response = _rabbitMQPublisher.Publish(intSvcRequest, GlobalConstants.MGTPORTAL_IN, GlobalConstants.MGTPORTAL_OUT);
        HandleResponse(response);

        return appConfig;
    }
}
