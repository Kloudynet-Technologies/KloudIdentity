namespace KN.KloudIdentity.Mapper.Domain;

public class AppSettings
{
    public RabbitMQOptions RabbitMQ { get; set; } = new RabbitMQOptions();
    public HangfireOptions Hangfire { get; set; } = new HangfireOptions();
    public string ExternalQueueEncryptionKey { get; set; } = string.Empty;
    public string ExternalQueueUrl { get; set; } = string.Empty;
    public UserMigrationOptions UserMigration { get; set; } = new UserMigrationOptions();
    public LicenseValidationOptions LicenseValidation { get; set; } = new LicenseValidationOptions();
    public List<string> DotRezAppIds { get; set; } = new List<string>();
    public UserKeyMappingOptions UserKeyMapping { get; set; } = new UserKeyMappingOptions();
    public List<UserKeyMappingConfig> UserKeyMappingConfig { get; set; } = [];
}

public class RabbitMQOptions
{
    public string Hostname { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string ExchangeName { get; set; } = string.Empty;
    public string[] QueueNames { get; set; } = Array.Empty<string>();
    public string QueueName_In { get; set; } = string.Empty;
    public string QueueName_Out { get; set; } = string.Empty;
    public int LogFilter { get; set; }
    public string CronExpression { get; set; } = string.Empty;
}

public class HangfireOptions
{
    public string RecurringJobCronExpression { get; set; } = string.Empty;
    public string RemoveJobCronExpression { get; set; } = string.Empty;
}

public class UserMigrationOptions
{
    public string AzureStorageTableName { get; set; } = string.Empty;
    public Dictionary<string, bool> AppFeatureEnabledMap { get; set; } = new Dictionary<string, bool>();
    public Dictionary<string, string> AppCorrelationPropertyMap { get; set; } = new Dictionary<string, string>();
    public string AzureStorageConnectionString { get; set; } = string.Empty;
}

public class LicenseValidationOptions
{
    public string CacheKey { get; set; } = "LicenseStatus";
    public int CacheDurationMinutes { get; set; } = 60;
}

public class UserKeyMappingOptions
{
    public string AzureStorageConnectionString { get; set; } = string.Empty;
}

public class UserKeyMappingConfig
{
    public string AppId { get; set; } = string.Empty;  

    public string AzureStorageTableName { get; set; } = string.Empty;
}