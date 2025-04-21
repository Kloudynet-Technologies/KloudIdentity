using KN.KloudIdentity.Mapper.Domain.Mapping;

namespace KN.KloudIdentity.Mapper.Domain.Mapping.Inbound;

public record InboundMappingConfig(
    string InboundAttMappingUsersPath,
    List<InboundAttributeMapping> InboundAttributeMappings
);

public record InboundAttributeMapping(
    string Id,
    MappingTypes MappingType,
    AttributeDataTypes DataType,
    string ValuePath,
    string EntraIdAttribute,
    bool IsRequired,
    string DefaultValue
);