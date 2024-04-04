namespace KN.KloudIdentity.Mapper.Domain;

public class AppSettings
{
    public RabbitMQOptions RabbitMQ { get; set; } = new RabbitMQOptions();
}

public class RabbitMQOptions
{
    public string Hostname { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string ExchangeName { get; set; } = string.Empty;
    public string[] QueueNames { get; set; } = Array.Empty<string>();
    public string QueueName_In { get; set; } = string.Empty;
    public string QueueName_Out { get; set; } = string.Empty;
    public int LogFilter { get; set; }
}