using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Messaging;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.Messaging;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Queries;

public class ListApplicationsQuery : IListApplicationsQuery
{
    private readonly MessageBroker _rabbitMQPublisher;
    public ListApplicationsQuery(MessageBroker rabbitMQPublisher, RabbitMQUtil rabbitMQUtil)
    {
        _rabbitMQPublisher = rabbitMQPublisher;
    }
    public async Task<IList<Application>> ListAsync(CancellationToken cancellationToken = default)
    {
       
        var correlationId = Guid.NewGuid().ToString();
        var intSvcRequest = new InterserviceMessage(string.Empty, correlationId, Action: MessageType.ListInboundApplications.ToString());


        // Consume the response from the message broker
        IList<Application> applications = null;
        void HandleResponse(InterserviceMessage? response)
        {
            if (response != null)
            {
                if (!response.IsError)
                {
                    applications = JsonSerializer.Deserialize<IList<Application>>(response.Message, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    _rabbitMQPublisher.Close();
                }
            }
        }

        var response = _rabbitMQPublisher.Publish(intSvcRequest, GlobalConstants.MGTPORTAL_IN, GlobalConstants.MGTPORTAL_OUT);
        HandleResponse(response);

        return applications;
    }
}
