using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Domain.Setting;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.Messaging;
using System.Text.Json;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Queries;

public class GetApplicationSettingQuery : IGetApplicationSettingQuery
{
    private readonly MessageBroker _messageBroker;
    public GetApplicationSettingQuery(MessageBroker messageBroker)
    {
        _messageBroker = messageBroker;
    }
    public async Task<ApplicationSetting?> GetAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ArgumentNullException(nameof(appId));
        }

        InterserviceMessage response = null;
        var correlationId = Guid.NewGuid().ToString();
        var message = new InterserviceMessage
        (
            JsonSerializer.Serialize(new { AppId = appId }),
            correlationId,
            false,
            null,
            MessageType.GetApplicationSetting.ToString()
        );

        ApplicationSetting applicationSetting = null;

        response = _messageBroker.Publish(message, GlobalConstants.MGTPORTAL_IN, GlobalConstants.MGTPORTAL_OUT);
        if (response != null && !response.IsError)
        {
            applicationSetting = JsonSerializer.Deserialize<ApplicationSetting>(response.Message, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            _messageBroker.Close();
        }

        return applicationSetting;
    }
}
