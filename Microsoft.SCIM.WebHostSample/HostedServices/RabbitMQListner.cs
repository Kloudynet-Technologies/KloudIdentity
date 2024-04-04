using System;
using System.Threading;
using System.Threading.Tasks;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.Messaging;
using KN.KloudIdentity.Mapper.MapperCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace Microsoft.SCIM.WebHostSample;

public class RabbitMQListner : IHostedService
{
    private readonly string _queueName_In = string.Empty;
    private readonly string _queueName_Out = string.Empty;
    private readonly MessageBroker _messageBroker;
    private readonly IGetVerifiedAttributeMapping _getVerifiedAttributeMapping;

    public RabbitMQListner(string queueName_In,
                string queueName_Out,
                MessageBroker messageBroker, IServiceScopeFactory serviceScopeFactory)
    {
        _queueName_In = queueName_In;
        _queueName_Out = queueName_Out;

        _messageBroker = messageBroker;

        var scope = serviceScopeFactory.CreateScope();
        _getVerifiedAttributeMapping = scope.ServiceProvider.GetRequiredService<IGetVerifiedAttributeMapping>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _messageBroker.Consume(_queueName_In, "", HandleMessage, cancellationToken);
    }

    private async void HandleMessage(InterserviceMessage message = null)
    {
        if (message != null)
        {
            var verifyingRequest = JsonConvert.DeserializeAnonymousType(message.Message, new { AppId = "", Type = 0, Direction = 0, Method = 0 });
            InterserviceMessage outMessage = null;

            try
            {
                var response = await _getVerifiedAttributeMapping.GetVerifiedAsync(verifyingRequest.AppId,
                                                                        (ObjectTypes)verifyingRequest.Type,
                                                                        (SCIMDirections)verifyingRequest.Direction,
                                                                        (HttpRequestTypes)verifyingRequest.Method);

                if (response == null)
                {
                    outMessage = new InterserviceMessage("Response is null.", message.CorrelationId, true);
                }

                outMessage = new InterserviceMessage(response.ToString(), message.CorrelationId);
            }
            catch (Exception ex)
            {
                outMessage = new InterserviceMessage(ex.Message, message.CorrelationId, true);
            }
            finally
            {
                _messageBroker.Publish(outMessage, _queueName_Out);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
