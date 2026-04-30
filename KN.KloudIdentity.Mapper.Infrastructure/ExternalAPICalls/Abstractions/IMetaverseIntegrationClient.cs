// File: Infrastructure/ExternalAPICalls/Abstractions/IMetaverseIntegrationClient.cs

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;

public interface IMetaverseIntegrationClient
{
    /// <summary>
    /// Sends a request to the metaverse integration service to perform a create operation for the specified application and payload.
    /// </summary>
    Task<T> CreateAsync<T>(string tenantId, string appId, object payload, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request to the metaverse integration service to perform a get operation for the specified application and identifier.
    /// </summary>
    Task<T> GetAsync<T>(string tenantId, string appId, string identifier, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request to the metaverse integration service to perform an update operation for the specified application, identifier, and payload.
    /// </summary>
    Task<T> UpdateAsync<T>(string tenantId, string appId, string identifier, object payload, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request to the metaverse integration service to perform a replace operation for the specified application, identifier, and payload.
    /// </summary>
    Task<T> ReplaceAsync<T>(string tenantId, string appId, string identifier, object payload, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a request to the metaverse integration service to perform a delete operation for the specified application and identifier.
    /// </summary>
    Task<T> DeleteAsync<T>(string tenantId, string appId, string identifier, string correlationId, CancellationToken cancellationToken = default);
}