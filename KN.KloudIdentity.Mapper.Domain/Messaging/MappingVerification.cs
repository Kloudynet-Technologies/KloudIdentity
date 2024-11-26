using KN.KloudIdentity.Mapper.Domain.Mapping;

namespace KN.KloudIdentity.Mapper.Domain.Messaging;

public record MappingVerification(string AppId,string correlationId, ObjectTypes ObjectType, HttpRequestTypes HttpMethod);