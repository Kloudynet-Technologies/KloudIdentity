// File: Infrastructure/ExternalAPICalls/Abstractions/IMetaverseIntegrationClient.cs

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;

public interface IMetaverseIntegrationClient
{
    Task<T> CreateAsync<T>(string appId, object payload, string correlationId, CancellationToken cancellationToken = default);

    Task<T> GetAsync<T>(string appId, string identifier, string correlationId, CancellationToken cancellationToken = default);

    Task<T> UpdateAsync<T>(string appId, string identifier, object payload, string correlationId, CancellationToken cancellationToken = default);

    Task<T> ReplaceAsync<T>(string appId, string identifier, object payload, string correlationId, CancellationToken cancellationToken = default);

    Task<T> DeleteAsync<T>(string appId, string identifier, string correlationId, CancellationToken cancellationToken = default);
}