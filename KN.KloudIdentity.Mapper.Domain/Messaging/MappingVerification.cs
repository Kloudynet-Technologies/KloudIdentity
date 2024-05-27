using KN.KloudIdentity.Mapper.Domain.Mapping;

namespace KN.KloudIdentity.Mapper.Domain.Messaging;

public record MappingVerification(string AppId, ObjectTypes ObjectType, SCIMDirections Direction, HttpRequestTypes HttpMethod);