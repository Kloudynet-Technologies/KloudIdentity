using System.Net.Http.Headers;
using System.Text.Json;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.Messaging;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Queries;

public class GetFullAppConfigQuery : IGetFullAppConfigQuery
{
    private readonly RabbitMQPublisher _rabbitMQPublisher;
    public GetFullAppConfigQuery(RabbitMQPublisher rabbitMQPublisher)
    {
        _rabbitMQPublisher = rabbitMQPublisher;
    }

    public async Task<AppConfig?> GetAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ArgumentNullException(nameof(appId));
        }

        string? response = null;
        var correlationId = Guid.NewGuid().ToString();
        _rabbitMQPublisher.Publish(appId, correlationId);

        response = _rabbitMQPublisher.Consume(correlationId);

        return JsonSerializer.Deserialize<AppConfig>(response, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}
