using Azure.Data.Tables;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Infrastructure.CEB.Abstractions;

namespace KN.KloudIdentity.Mapper.Infrastructure.CEB.Queries;

public class GetUserDetailsFromStorageQuery : IGetUserDetailsFromStorageQuery
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly AppSettings _appSettings;

    public GetUserDetailsFromStorageQuery(string connectionString, AppSettings appSettings)
    {
        var storageConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _tableServiceClient = new TableServiceClient(storageConnectionString);
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
    }

    public async Task<UserKeyMappingData?> GetUserKeyDataAsync(string username)
    {
        var tableName = _appSettings.UserKeyMappingConfig.FirstOrDefault()?.AzureStorageTableName;
        if (string.IsNullOrWhiteSpace(tableName))
            throw new InvalidOperationException("Azure Storage table name is not configured.");

        var tableClient = _tableServiceClient.GetTableClient(tableName) ??
                          throw new InvalidOperationException("Table client is not initialized.");
        await tableClient.CreateIfNotExistsAsync();

        // QueryAsync returns an AsyncPageable<TableEntity>
        var queryResult = tableClient.QueryAsync<TableEntity>(entity => entity.GetString("Username") == username);

        await foreach (var entity in queryResult)
        {
            // Return the first matching record
            return new UserKeyMappingData(
                entity.PartitionKey,
                entity.RowKey,
                entity.GetString("UserKey") ?? string.Empty,
                entity.GetString("Username") ?? string.Empty
            );
        }

        // No match found
        return null;
    }
}
