using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Queries;

public class GetFullAppConfigQuery : IGetFullAppConfigQuery
{
    private readonly IRequestClient<IMgtPortalServiceRequestMsg> _requestClient;
    public GetFullAppConfigQuery(
        IServiceScopeFactory serviceScopeFactory
        )
    {
        using var serviceScope = serviceScopeFactory.CreateScope();
        _requestClient = serviceScope.ServiceProvider.GetRequiredService<IRequestClient<IMgtPortalServiceRequestMsg>>();
    }

    public async Task<AppConfig?> GetAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ArgumentNullException(nameof(appId));
        }

        return await SendMessageAndProcessResponse(appId);
    }

    private async Task<AppConfig> SendMessageAndProcessResponse(string appId)
    {
        var message = new MgtPortalServiceRequestMsg(
                       appId,
                       ActionType.GetFullApplication.ToString(),
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

    private static AppConfig ProcessResponse(IInterserviceResponseMsg? response)
    {
        if (response == null || response.IsError == true)
        {
            throw new InvalidOperationException($"{response?.ErrorMessage ?? "Unknown error"}", response?.ExceptionDetails);
        }

        var appConfig = JsonConvert.DeserializeObject<AppConfig>(response.Message);

        return appConfig ?? throw new KeyNotFoundException("Application configuration not found");
    }
}
