using System.Text;
using System.Text.Json;
using KN.KloudIdentity.Mapper.Domain;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace KN.KloudIdentity.Mapper.Infrastructure.Messaging;

public class MessageBroker : IDisposable
{
    private readonly IModel _channel;
    private readonly string _exchangeName;

    public MessageBroker(
                        string exchangeName,
                    string[] queueNames,
                    RabbitMQUtil rabbitMQUtil)
    {
        _exchangeName = exchangeName;

        _channel = rabbitMQUtil.GetChannel();

        _channel.ExchangeDeclare(exchange: _exchangeName, type: "direct");

        foreach (var queueName in queueNames)
        {
            _channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
            _channel.QueueBind(queue: queueName, exchange: _exchangeName, routingKey: queueName);
        }
    }

    public void Publish(InterserviceMessage message, string queueName)
    {
        string messageStr = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(messageStr);
        var properties = _channel.CreateBasicProperties();
        properties.CorrelationId = message.CorrelationId;

        _channel.BasicPublish(exchange: _exchangeName, routingKey: queueName, basicProperties: properties, body: body);
    }

    public async Task Consume(string queueName, string correlationId = "", Action<InterserviceMessage?> callback = null, CancellationToken cancellationToken = default)
    {
        CancellationTokenSource cts = new CancellationTokenSource();

        if (cancellationToken == default)
        {
            cancellationToken = cts.Token;
        }

        var consumer = new EventingBasicConsumer(_channel);
        InterserviceMessage? response = null; // Declare a variable to hold the response
        string consumerTag = string.Empty;

        cancellationToken.Register(() => response = default); // Register a callback to set the response to default

        // Run the consumer in a separate task
        var consumerTask = Task.Run(async () =>
        {
            consumer.Received += (model, ea) =>
            {
#if DEBUG
                Console.WriteLine(" [x] Received message.");
#endif

                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                if (string.IsNullOrEmpty(correlationId) || ea.BasicProperties.CorrelationId == correlationId) // If correlationId is empty or matches the correlationId in the message, process the message
                {
#if DEBUG
                    Console.WriteLine(" [x] Received {0}", message);
#endif
                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);

                    response = JsonSerializer.Deserialize<InterserviceMessage>(message);
                }

                callback?.Invoke(response); // Invoke the callback with the response

                if (!string.IsNullOrEmpty(correlationId)) // If correlationId is not empty, cancel the consumer
                {
                    consumer.Received -= (model, ea) => { };
                    cts.Cancel();
                    _channel.BasicCancel(consumerTag);
#if DEBUG
                    Console.WriteLine(" [x] Consumer cancelled.");
#endif
                }
            };

            consumerTag = _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
#if DEBUG
            Console.WriteLine(" [*] Waiting for messages.");
#endif
        }, cancellationToken);

        await consumerTask;
    }

    public void Close()
    {
    }

    public void Dispose()
    {
        Close();
    }
}
