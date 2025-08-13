using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;

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
            Log.Error(ex,
                "An error occurred while processing the request for App ID: {AppId}. Error Message: {ErrorMessage}",
                appId, ex.Message);
            throw new InvalidOperationException(ex.Message);
        }
    }

    private static AppConfig ProcessResponse(IInterserviceResponseMsg? response)
    {
        if (response == null || response.IsError == true)
        {
            Log.Error("Error processing response: {ErrorMessage}. Exception Details: {ExceptionDetails}",
                response?.ErrorMessage ?? "Unknown error", response?.ExceptionDetails);
            throw new InvalidOperationException($"{response?.ErrorMessage ?? "Unknown error"}",
                response?.ExceptionDetails);
        }

        var appConfig = JsonConvert.DeserializeObject<AppConfig>(response.Message);

        return appConfig ?? throw new KeyNotFoundException("Application configuration not found");
    }
}