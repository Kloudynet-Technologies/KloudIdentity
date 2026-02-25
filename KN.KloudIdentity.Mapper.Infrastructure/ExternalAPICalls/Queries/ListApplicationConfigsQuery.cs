using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using MassTransit;
using Newtonsoft.Json;
using Serilog;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Queries;

public class ListApplicationConfigsQuery : IListApplicationConfigsQuery
{
    private readonly IRequestClient<IMgtPortalServiceRequestMsg> _requestClient;

    public ListApplicationConfigsQuery(IRequestClient<IMgtPortalServiceRequestMsg> requestClient)
    {
        _requestClient = requestClient;
    }

    public async Task<List<AppConfig>> ListAsync(CancellationToken cancellationToken = default)
    {
        var message = new MgtPortalServiceRequestMsg(
            string.Empty,
            ActionType.ListApplicationConfigs.ToString(),
            Guid.NewGuid().ToString(),
            null
        );

        try
        {
            var response = await _requestClient.GetResponse<IInterserviceResponseMsg>(
                message,
                timeout: RequestTimeout.After(s: 30),
                cancellationToken: cancellationToken);

            return ProcessResponse(response.Message);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Request failed: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    private static List<AppConfig> ProcessResponse(IInterserviceResponseMsg? response)
    {
        if (response == null || response.IsError == true)
            throw new InvalidOperationException(response?.ErrorMessage ?? "Unknown error");

        var appConfigs = JsonConvert.DeserializeObject<List<AppConfig>>(response.Message);
        return appConfigs ?? throw new KeyNotFoundException("Application configuration not found");
    }
}