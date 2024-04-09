using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public interface IAPIMapperBaseInbound<T> where T : class
{
    /// <summary>
    /// Gets or sets the ID of the application.
    /// </summary>
    // string AppId { get; set; }

    /// <summary>
    /// SCIM object coming from the Azure AD.
    /// </summary>
    // T Resource { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID for Azure AD.
    /// </summary>
    // string? CorrelationID { get; set; }

    /// <summary>
    /// Gets or sets the payload to be sent to the API.
    /// </summary>
    // JObject Payload { get; set; }

    /// <summary>
    /// Gets the application configuration asynchronously.
    /// </summary>
    /// <returns></returns>
    Task<AppConfig> GetAppConfigAsync(string appId, string correlationId);

    /// <summary>
    /// Gets the authentication token asynchronously.
    /// </summary>
    /// <returns></returns>
    Task<string> GetAuthenticationAsync(AppConfig config, SCIMDirections direction);

    /// <summary>
    /// Map and prepare the payload to be sent to the API asynchronously.
    /// </summary>
    /// <returns></returns>
    Task<T> MapAndPreparePayloadAsync(IList<AttributeSchema> schema, JObject resource);
}
