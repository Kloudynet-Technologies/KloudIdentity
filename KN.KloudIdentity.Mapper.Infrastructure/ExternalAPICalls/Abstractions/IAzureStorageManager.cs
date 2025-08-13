using System;
using KN.KloudIdentity.Mapper.Domain.Application;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;

public interface IAzureStorageManager
{
    Task<bool> CreateUserMigrationDataAsync(UserMigrationData userMigrationData);
    Task<UserMigrationData?> GetUserMigrationDataAsync(string partitionKey, string correlationId);
    Task<bool> DeleteUserMigrationDataAsync(string partitionKey, string correlationId);
    Task<IEnumerable<UserMigrationData>> GetAllUserMigrationDataAsync();
}
