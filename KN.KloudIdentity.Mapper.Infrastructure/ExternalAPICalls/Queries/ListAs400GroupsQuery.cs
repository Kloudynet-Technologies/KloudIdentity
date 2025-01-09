using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.Domain.As400;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Queries;

public class ListAs400GroupsQuery : IListAs400GroupsQuery
{
    private readonly IRequestClient<IMetaverseServiceRequestMsg> _requestClient;
    public ListAs400GroupsQuery(
        IServiceScopeFactory serviceScopeFactory
    )
    {
        using var serviceScope = serviceScopeFactory.CreateScope();
        _requestClient = serviceScope.ServiceProvider.GetRequiredService<IRequestClient<IMetaverseServiceRequestMsg>>();
    }

    public async Task<IList<As400Group>> ListAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ArgumentNullException(nameof(appId));
        }

        return await SendMessageAndProcessResponse(appId);
    }

    private async Task<IList<As400Group>> SendMessageAndProcessResponse(string appId)
    {
        var message = new MetaverseServiceRequestMsg(
            appId,
            ActionType.ListAs400Groups.ToString(),
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

    private static IList<As400Group> ProcessResponse(IInterserviceResponseMsg? response)
    {
        if (response == null || response.IsError == true)
        {
            throw new InvalidOperationException($"{response?.ErrorMessage ?? "Unknown error"}", response?.ExceptionDetails);
        }

        var groups = JsonConvert.DeserializeObject<IList<As400Group>>(response.Message);

        return groups ?? new List<As400Group>();
    }
}