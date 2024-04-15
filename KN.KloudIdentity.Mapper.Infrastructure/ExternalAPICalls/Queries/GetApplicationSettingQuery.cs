using KN.KloudIdentity.Mapper.Domain.Setting;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.Messaging;
using System.Text.Json;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Queries;

public class GetApplicationSettingQuery : IGetApplicationSettingQuery
{
    private readonly RabbitMQPublisher _rabbitMQPublisher;
    public GetApplicationSettingQuery(RabbitMQPublisher rabbitMQPublisher)
    {
        _rabbitMQPublisher = rabbitMQPublisher;
    }
    public async Task<ApplicationSetting?> GetAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ArgumentNullException(nameof(appId));
        }

        string? response = null;
        var correlationId = Guid.NewGuid().ToString();
        var message = new AppMessage
        {
            AppId = appId,
            MessageType = MessageType.GetApplicationSetting
        };
        _rabbitMQPublisher.Publish(JsonSerializer.Serialize(message), correlationId);

        response = _rabbitMQPublisher.Consume(correlationId);

        return JsonSerializer.Deserialize<ApplicationSetting>(response, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}
