using KN.KloudIdentity.Mapper.Domain.ExternalEndpoint;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic
{
    public interface IOutboundPayloadProcessor
    {
        /// <summary>
        /// Custom logic implementation for outbound.
        /// </summary>
        /// <param name="payload">Payload to be processed.</param>
        /// <param name="endpointInfo">External endpoint information.</param>
        /// <param name="correlationID">Correlation ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        Task<dynamic> ProcessAsync(dynamic payload, ExternalEndpointInfo endpointInfo, string correlationID, CancellationToken cancellationToken);
    }
}
