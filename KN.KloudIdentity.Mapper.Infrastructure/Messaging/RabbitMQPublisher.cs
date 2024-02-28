using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace KN.KloudIdentity.Mapper.Infrastructure.Messaging;

public class RabbitMQPublisher : IDisposable
{
    private readonly ConnectionFactory _factory;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _exchangeName;
    private readonly string _queueName_Out;
    private readonly string _queueName_In;

    public RabbitMQPublisher(string username, string password, string host, string exchangeName, string queueName_Out, string queueName_In)
    {
        _exchangeName = exchangeName;
        _queueName_Out = queueName_Out;
        _queueName_In = queueName_In;

        _factory = new ConnectionFactory() { HostName = host, UserName = username, Password = password, VirtualHost = "/" };
        _connection = _factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(exchange: _exchangeName, type: "direct");
        _channel.QueueDeclare(queue: _queueName_In, durable: false, exclusive: false, autoDelete: false, arguments: null);
        _channel.QueueDeclare(queue: _queueName_Out, durable: false, exclusive: false, autoDelete: false, arguments: null);

        _channel.QueueBind(queue: _queueName_In, exchange: _exchangeName, routingKey: _queueName_In);
        _channel.QueueBind(queue: _queueName_Out, exchange: _exchangeName, routingKey: _queueName_Out);
    }

    public void Publish(string message, string correlationId)
    {
        var body = Encoding.UTF8.GetBytes(message);
        var properties = _channel.CreateBasicProperties();
        properties.CorrelationId = correlationId;

        _channel.BasicPublish(exchange: _exchangeName, routingKey: _queueName_In, basicProperties: properties, body: body);
    }

    public string? Consume(string correlationId)
    {
        var consumer = new EventingBasicConsumer(_channel);
        string? response = null; // Declare a variable to hold the response

        consumer.Received += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            if (ea.BasicProperties.CorrelationId == correlationId)
            {
                Console.WriteLine(" [x] Received {0}", message);
                _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);

                response = message;
            }
        };

        _channel.BasicConsume(queue: _queueName_Out, autoAck: false, consumer: consumer);

        while (response == null)
        {
            // Wait for the response
            Thread.Sleep(100);
        }

        return response;
    }

    public void Close()
    {
        _connection.Close();
    }

    public void Dispose()
    {
        Close();
    }
}
