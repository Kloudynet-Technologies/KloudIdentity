using KN.KloudIdentity.Mapper.Domain.ExternalEndpoint;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.Outbound.CustomLogic
{
    public interface ICustomLogic
    {
        /// <summary>
        /// Custom logic implementation for outbound.
        /// </summary>
        /// <param name="payload">Payload to be processed.</param>
        /// <param name="endpointInfo">External endpoint information.</param>
        /// <param name="correlationID">Correlation ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        Task<JObject> ProcessAsync(JObject payload, ExternalEndpointInfo endpointInfo, string correlationID, CancellationToken cancellationToken);
    }
}
