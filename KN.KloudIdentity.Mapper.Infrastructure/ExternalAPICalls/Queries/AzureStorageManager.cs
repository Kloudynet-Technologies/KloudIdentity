using System;
using Azure.Data.Tables;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Queries;

public class AzureStorageManager : IAzureStorageManager
{
    private readonly string _connectionString;
    private readonly TableServiceClient _tableServiceClient;

    public AzureStorageManager(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _tableServiceClient = new TableServiceClient(_connectionString);
    }

    public Task<bool> CreateUserMigrationDataAsync(UserMigrationData userMigrationData)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Retrieves user migration data based on partition key and correlation ID.
    /// This method is used to fetch the migration data for a specific user.
    /// Data retrieval from the Azure Storage table.
    /// </summary>
    /// <param name="partitionKey"></param>
    /// <param name="correlationId"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<UserMigrationData?> GetUserMigrationDataAsync(string partitionKey, string correlationId)
    {
        if (string.IsNullOrWhiteSpace(partitionKey)) throw new ArgumentNullException(nameof(partitionKey));
        if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentNullException(nameof(correlationId));

        var tableClient = _tableServiceClient.GetTableClient("UserMigrationData") ?? throw new InvalidOperationException("Table client is not initialized.");
        await tableClient.CreateIfNotExistsAsync();

        var queryResult = tableClient.QueryAsync<TableEntity>(entity =>
            entity.PartitionKey == partitionKey && entity.GetString("CorrelationId") == correlationId);

        await foreach (var entity in queryResult)
        {
            return new UserMigrationData(
                entity.PartitionKey,
                entity.RowKey,
                entity.GetString("CorrelationId"));
        }

        return null;
    }

    public Task<bool> DeleteUserMigrationDataAsync(string partitionKey, string correlationId)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<UserMigrationData>> GetAllUserMigrationDataAsync()
    {
        throw new NotImplementedException();
    }
}
