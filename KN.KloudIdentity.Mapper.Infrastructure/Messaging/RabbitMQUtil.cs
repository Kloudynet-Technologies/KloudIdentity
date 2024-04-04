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
    }

    private IModel CreateInstance()
    {
        var channel = _connection.CreateModel();

        return channel;
    }

    public IModel GetChannel()
    {
        var channel = CreateInstance();
        return channel;
    }

    public void Dispose()
    {
        _connection.Close();
    }
}
