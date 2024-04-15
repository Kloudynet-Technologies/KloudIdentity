using System.Dynamic;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Configuration;
using KN.KloudIdentity.Mapper.Domain;
using Microsoft.Extensions.Options;

namespace KN.KloudIdentity.Mapper.Infrastructure.Messaging;

public class RabbitMQUtil : IDisposable
{
    private static IConnection _connection;
    private static IModel _channel;
    private static ConnectionFactory _factory;
    private static AppSettings _appSettings;

    public RabbitMQUtil(IOptions<AppSettings> appSettings)
    {
        _appSettings = appSettings.Value;

        _factory = new ConnectionFactory()
        {
            HostName = _appSettings.RabbitMQ.Hostname,
            UserName = _appSettings.RabbitMQ.UserName,
            Password = _appSettings.RabbitMQ.Password,
            VirtualHost = "/"
        };

        _connection = _factory.CreateConnection();
        _channel = _connection.CreateModel();

        DeclareAndBindQueue(_appSettings.RabbitMQ.ExchangeName, _appSettings.RabbitMQ.QueueNames);
    }

    private void DeclareAndBindQueue(string exchangeName, string[] queueNames)
    {
        _channel.ExchangeDeclare(exchange: exchangeName, type: "direct");

        foreach (var queueName in queueNames)
        {
            _channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
            _channel.QueueBind(queue: queueName, exchange: exchangeName, routingKey: queueName);
        }
    }

    private IModel CreateInstance()
    {
        if (_connection == null || !_connection.IsOpen)
            _connection = _factory.CreateConnection();

        return _channel;
    }

    public IModel GetChannel()
    {
        var channel = CreateInstance();
        return channel;
    }

    public void Dispose()
    {
        if (_connection != null && _connection.IsOpen)
            _connection.Close();
    }
}
