// File: Infrastructure/ExternalAPICalls/Abstractions/IMetaverseIntegrationClient.cs

using KN.KloudIdentity.Mapper.Domain.Itsm;
using KN.KloudIdentity.Mapper.Domain.Messaging;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;

public interface IMetaverseIntegrationClient
{
    /// <summary>
    /// Sends a request to the metaverse integration service to perform a create operation for the specified application and payload.
    /// </summary>
    Task<T> SendAsync<T>(string payload, string correlationId, ActionType action,
        CancellationToken cancellationToken = default);
}