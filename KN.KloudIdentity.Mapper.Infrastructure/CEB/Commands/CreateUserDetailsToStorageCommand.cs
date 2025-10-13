using Azure.Data.Tables;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Infrastructure.CEB.Abstractions;


namespace KN.KloudIdentity.Mapper.Infrastructure.CEB.Commands;

public class CreateUserDetailsToStorageCommand : ICreateUserDetailsToStorageCommand
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly AppSettings _appSettings;

    public CreateUserDetailsToStorageCommand(string connectionString, AppSettings appSettings)
    {
        var storageConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _tableServiceClient = new TableServiceClient(storageConnectionString);
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
    }

    public async Task<bool> CreateUserKeyDataAsync(string userKey, string username)
    {
        if (string.IsNullOrWhiteSpace(userKey))
            throw new ArgumentException("UserKey cannot be null or empty.", nameof(userKey));
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be null or empty.", nameof(username));

        var tableName = _appSettings.UserKeyMappingConfig.FirstOrDefault()?.AzureStorageTableName;
        if (string.IsNullOrWhiteSpace(tableName))
            throw new InvalidOperationException("Azure Storage table name is not configured.");

        var tableClient = _tableServiceClient.GetTableClient(tableName);
        await tableClient.CreateIfNotExistsAsync();
  
        var entity = new TableEntity
        {
            PartitionKey = "UserKeyMapping",
            RowKey = Guid.NewGuid().ToString(),
            ["UserKey"] = userKey,
            ["Username"] = username
        };

        try
        {
            await tableClient.AddEntityAsync(entity);
            return true;
        }
        catch
        {  
            return false;
        }
    }
}