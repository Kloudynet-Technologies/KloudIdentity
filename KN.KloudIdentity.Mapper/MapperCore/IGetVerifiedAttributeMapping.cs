using KN.KloudIdentity.Common.Enum;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.AspNetCore.Http;

namespace KN.KloudIdentity.Mapper.MapperCore;

public interface IGetVerifiedAttributeMapping
{
    Task<dynamic> GetVerifiedAsync(string appId, MappingType type,SCIMDirections direction, HttpRequestTypes httpRequestType);
}
