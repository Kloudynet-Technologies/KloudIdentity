using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Queries;

public class ListApplicationsQuery : IListApplicationsQuery
{
    private readonly IRequestClient<IMgtPortalServiceRequestMsg> _requestClient;
    public ListApplicationsQuery(
        IServiceScopeFactory serviceScopeFactory
        )
    {
        using var serviceScope = serviceScopeFactory.CreateScope();
        _requestClient = serviceScope.ServiceProvider.GetRequiredService<IRequestClient<IMgtPortalServiceRequestMsg>>();
    }
    public async Task<IList<Application>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await SendMessageAndProcessResponse();
    }

    private async Task<IList<Application>> SendMessageAndProcessResponse()
    {
        var message = new MgtPortalServiceRequestMsg(
                       string.Empty,
                       ActionType.ListInboundApplications.ToString(),
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

    private static IList<Application> ProcessResponse(IInterserviceResponseMsg? response)
    {
        if (response == null || response.IsError == true)
        {
            throw new InvalidOperationException($"{response?.ErrorMessage ?? "Unknown error"}", response?.ExceptionDetails);
        }

        var applications = JsonConvert.DeserializeObject<IList<Application>>(response.Message);

        return applications ?? throw new KeyNotFoundException("Applications not found");
    }
}
