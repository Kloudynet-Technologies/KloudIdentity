using System;
using KN.KloudIdentity.Mapper.Domain.Messaging;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;

public interface IReqStagQueuePublisher
{
    /// <summary>
    /// Publishes the request to staging queue and receives the response.
    /// </summary>
    /// <param name="request">Request to be published</param>
    /// <param name="correlationID">Correlation ID</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Response from the staging queue</returns>
    Task<string> SendAsync(string request, string correlationID, OperationTypes operationType, CancellationToken cancellationToken = default);
}
