using Azure.Data.Tables;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Shared;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Queries;

public class AzureStorageManager : IAzureStorageManager
{
    private const string ConnectionStringAuthMethod = "ConnectionString";
    private const string ServicePrincipalAuthMethod = "ServicePrincipal";

    private readonly TableServiceClient _tableServiceClient;
    private readonly AppSettings _appSettings;

    /// <summary>
    /// Supports Azure Storage authentication via <c>ConnectionString</c> or <c>ServicePrincipal</c>, as selected by <paramref name="authMethod"/>.
    /// </summary>
    /// <param name="connectionString">Required when <paramref name="authMethod"/> is <c>ConnectionString</c>; ignored for <c>ServicePrincipal</c>.</param>
    /// <param name="authMethod">The authentication method to use: <c>ConnectionString</c> or <c>ServicePrincipal</c>.</param>
    public AzureStorageManager(
        string? connectionString,
        string authMethod,
        AppSettings appSettings,
        IConfiguration configuration
        )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authMethod);
        ArgumentNullException.ThrowIfNull(configuration);

        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));

        if (authMethod.Equals(ConnectionStringAuthMethod, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Log.Error("Azure Storage connection string is required but not provided in configuration.");
                throw new ArgumentException(
                    "Azure Storage connection string is required when auth method is ConnectionString.",
                    nameof(connectionString));
            }

            _tableServiceClient = new TableServiceClient(connectionString);
        }
        else if (authMethod.Equals(ServicePrincipalAuthMethod, StringComparison.OrdinalIgnoreCase))
        {
            var accountUrl = _appSettings.UserMigration.AzureStorageAccountUrl;
            if (string.IsNullOrWhiteSpace(accountUrl))
            {
                Log.Error("Azure Storage account URL is required but not provided in configuration.");
                throw new InvalidOperationException("Azure Storage account URL is not configured.");
            }

            if (!Uri.TryCreate(accountUrl, UriKind.Absolute, out var parsedAccountUri) ||
                (parsedAccountUri.Scheme != Uri.UriSchemeHttps && parsedAccountUri.Scheme != Uri.UriSchemeHttp))
            {
                Log.Error("Invalid Azure Storage account URL configured: {AccountUrl}", accountUrl);
                throw new InvalidOperationException(
                    "Invalid Azure Storage account URL. Expected Azure Tables endpoint format, e.g. 'https://<account-name>.table.core.windows.net'.");
            }

            var credential = AzureCredentialHelper.CreateClientSecretCredential(configuration);
            _tableServiceClient = new TableServiceClient(parsedAccountUri, credential);
        }
        else
        {
            Log.Error("Unsupported Azure Storage authentication method: {AuthMethod}", authMethod);
            throw new ArgumentException(
                $"Unsupported auth method '{authMethod}'. Supported values are '{ConnectionStringAuthMethod}' and '{ServicePrincipalAuthMethod}'.",
                nameof(authMethod));
        }
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
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var tableName = _appSettings.UserMigration.AzureStorageTableName;
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new InvalidOperationException("Azure Storage table name is not configured.");
        }

        var tableClient = _tableServiceClient.GetTableClient(tableName);

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