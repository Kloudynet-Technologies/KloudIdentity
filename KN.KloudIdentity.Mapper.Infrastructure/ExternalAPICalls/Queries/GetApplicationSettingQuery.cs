using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Domain.Setting;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Queries;

public class GetApplicationSettingQuery : IGetApplicationSettingQuery
{
    private readonly IRequestClient<IMgtPortalServiceRequestMsg> _requestClient;
    public GetApplicationSettingQuery(
        IServiceScopeFactory serviceScopeFactory
        )
    {
        using var serviceScope = serviceScopeFactory.CreateScope();
        _requestClient = serviceScope.ServiceProvider.GetRequiredService<IRequestClient<IMgtPortalServiceRequestMsg>>();
    }

    public async Task<ApplicationSetting?> GetAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ArgumentNullException(nameof(appId));
        }

        return await SendMessageAndProcessResponse(appId);
    }

    private async Task<ApplicationSetting> SendMessageAndProcessResponse(string appId)
    {
        var message = new MgtPortalServiceRequestMsg(
                       appId,
                       ActionType.GetApplicationSetting.ToString(),
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

    private static ApplicationSetting ProcessResponse(IInterserviceResponseMsg? response)
    {
        if (response == null || response.IsError == true)
        {
            throw new InvalidOperationException($"{response?.ErrorMessage ?? "Unknown error"}", response?.ExceptionDetails);
        }

        var applications = JsonConvert.DeserializeObject<ApplicationSetting>(response.Message);

        return applications ?? throw new KeyNotFoundException("Application setting not found");
    }
}
