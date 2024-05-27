using System.Text;
using System.Text.Json;
using KN.KloudIdentity.Mapper.Domain;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace KN.KloudIdentity.Mapper.Infrastructure.Messaging;

public class MessageBroker : IDisposable
{
    private readonly IModel _channel;
    private string _exchangeName;
    private InterserviceMessage? _response;

    public MessageBroker(RabbitMQUtil rabbitMQUtil, string exchangeName)
    {
        _channel = rabbitMQUtil.GetChannel();
        _exchangeName = exchangeName;
    }

    public InterserviceMessage Publish(InterserviceMessage message, string publishQueueName, string consumeQueueName)
    {
        _response = null;

        var properties = _channel.CreateBasicProperties();
        properties.CorrelationId = message.CorrelationId;
        properties.ReplyTo = consumeQueueName;

        var msgStr = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(msgStr);

        _channel.BasicPublish(
            exchange: _exchangeName,
            routingKey: publishQueueName,
            basicProperties: properties,
            body: body);

        Consume(consumeQueueName, message.CorrelationId);

        return _response;
    }

    private void Consume(string queueName, string correlationId = "")
    {
        var consumer = new EventingBasicConsumer(_channel);
        string consumerTag = string.Empty;

        consumer.Received += (model, ea) =>
        {
            if (ea.BasicProperties.CorrelationId == correlationId)
            {
                _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);

                var message = Encoding.UTF8.GetString(ea.Body.ToArray());
                _response = JsonSerializer.Deserialize<InterserviceMessage>(message)!;

                _channel.BasicCancel(consumerTag);
            }
        };

        consumerTag = _channel.BasicConsume(
                                consumer: consumer,
                                queue: queueName,
                                autoAck: false);

        while (_response == null)
        {
            Thread.Sleep(50);
        }
    }

    public void Close()
    {
    }

    public void Dispose()
    {
        Close();
    }
}
