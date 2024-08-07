//using System;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using KN.KloudIdentity.Mapper.Domain;
//using KN.KloudIdentity.Mapper.Domain.Mapping;
//using KN.KloudIdentity.Mapper.MapperCore;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Hosting;
//using Newtonsoft.Json;
//using RabbitMQ.Client;
//using RabbitMQ.Client.Events;

//namespace Microsoft.SCIM.WebHostSample;

//public class RabbitMQListner : IHostedService
//{
//    private readonly string _queueName_In = string.Empty;
//    private readonly string _queueName_Out = string.Empty;
//    private readonly IModel _channel;
//    private string _consumerTag = string.Empty;
//    private string _exchangeName = string.Empty;
//    private readonly IGetVerifiedAttributeMapping _getVerifiedAttributeMapping;

//    public RabbitMQListner(string queueName_In,
//                string queueName_Out,
//                string exchangeName,
//                RabbitMQUtil rabbitMQUtil,
//                IServiceScopeFactory serviceScopeFactory)
//    {
//        _queueName_In = queueName_In;
//        _queueName_Out = queueName_Out;
//        _exchangeName = exchangeName;

//        _channel = rabbitMQUtil.GetChannel();

//        var scope = serviceScopeFactory.CreateScope();
//        _getVerifiedAttributeMapping = scope.ServiceProvider.GetRequiredService<IGetVerifiedAttributeMapping>();
//    }

//    public async Task StartAsync(CancellationToken cancellationToken)
//    {
//        _channel.QueueDeclare(queue: _queueName_In, durable: false, exclusive: false, autoDelete: false, arguments: null);

//        var consumer = new EventingBasicConsumer(_channel);

//        consumer.Received += (model, ea) =>
//        {
//            var body = ea.Body.ToArray();
//            var message = Encoding.UTF8.GetString(body);

//            var messageObj = System.Text.Json.JsonSerializer.Deserialize<InterserviceMessage>(message);

//            HandleMessage(messageObj);
//        };

//        _consumerTag = _channel.BasicConsume(queue: _queueName_In, autoAck: true, consumer: consumer);
//    }

//    private async void HandleMessage(InterserviceMessage message = null)
//    {
//        if (message != null)
//        {
//            var verifyingRequest = JsonConvert.DeserializeAnonymousType(message.Message, new { AppId = "", Type = 0, Direction = 0, Method = 0 });
//            InterserviceMessage outMessage = null;

//            try
//            {
//                var response = await _getVerifiedAttributeMapping.GetVerifiedAsync(verifyingRequest.AppId,
//                                                                        (ObjectTypes)verifyingRequest.Type,
//                                                                        (SCIMDirections)verifyingRequest.Direction,
//                                                                        (HttpRequestTypes)verifyingRequest.Method);

//                if (response == null)
//                {
//                    outMessage = new InterserviceMessage("Response is null.", message.CorrelationId, true);
//                }

//                var serializedResponse = System.Text.Json.JsonSerializer.Serialize(response);
//                outMessage = new InterserviceMessage(serializedResponse, message.CorrelationId);
//            }
//            catch (Exception ex)
//            {
//                outMessage = new InterserviceMessage(ex.Message, message.CorrelationId, true);
//            }
//            finally
//            {
//                PublishMessage(message, outMessage);
//            }
//        }
//    }

//    private void PublishMessage(InterserviceMessage message, InterserviceMessage outMessage)
//    {
//        var properties = _channel.CreateBasicProperties();
//        properties.CorrelationId = message.CorrelationId;

//        var msgStr = System.Text.Json.JsonSerializer.Serialize(outMessage);
//        var body = Encoding.UTF8.GetBytes(msgStr);

//        _channel.BasicPublish(
//            exchange: _exchangeName,
//            routingKey: _queueName_Out,
//            basicProperties: properties,
//            body: body);
//    }

//    public Task StopAsync(CancellationToken cancellationToken)
//    {
//        _channel.BasicCancel(_consumerTag);

//        return Task.CompletedTask;
//    }
//}
