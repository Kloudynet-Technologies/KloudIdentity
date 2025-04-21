using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Inbound;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Domain.Mapping.Inbound;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public interface IAPIMapperBaseInbound
{
    /// <summary>
    /// Gets or sets the correlation ID for Azure AD.
    /// </summary>
    string CorrelationID { get; init; }

    /// <summary>
    /// Gets the application configuration asynchronously.
    /// </summary>
    /// <returns></returns>
    Task<InboundConfig> GetAppConfigAsync(string appId);

    /// <summary>
    /// Gets the authentication token asynchronously.
    /// </summary>
    /// <returns></returns>
    Task<string> GetAuthenticationAsync(InboundConfig config, SCIMDirections direction);

    /// <summary>
    /// Map and prepare the payload to be sent to the API asynchronously.
    /// </summary>
    /// <returns></returns>
    Task<JObject> MapAndPreparePayloadAsync(InboundMappingConfig config, JObject users, string appId);
}
