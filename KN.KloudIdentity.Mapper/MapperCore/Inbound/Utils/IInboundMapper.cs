using KN.KloudIdentity.Mapper.Domain.Mapping.Inbound;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound.Utils;

public interface IInboundMapper
{
    /// <summary>
    /// Maps the users payload based on the mapping configurations
    /// </summary>
    Task<JObject> MapAsync(InboundMappingConfig mappingConfig, JObject usersPayload, string correlationId);

    /// <summary>
    /// Validates the mapping configurations.
    /// Returns a tuple with the first item as a boolean indicating if the mapping configurations are valid or not.
    /// The second item is an array of error messages if the mapping configurations are invalid.
    /// </summary>
    Task<(bool, string[])> ValidateMappingConfigAsync(InboundMappingConfig mappingConfig);

    /// <summary>
    /// Validates the mapped payload.
    /// Returns a tuple with the first item as a boolean indicating if the mapped payload is valid or not.
    /// The second item is an array of error messages if the mapped payload is invalid.
    /// </summary>
    Task<(bool, string[])> ValidateMappedPayloadAsync(JObject payload);

    /// <summary>
    /// Gets the SCIM payload template.
    /// </summary>
    Task<JObject> GetSCIMPayloadTemplateAsync();
}
