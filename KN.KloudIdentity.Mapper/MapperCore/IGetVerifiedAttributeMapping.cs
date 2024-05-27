using KN.KloudIdentity.Common.Enum;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore;

public interface IGetVerifiedAttributeMapping
{
    Task<JObject> GetVerifiedAsync(string appId, ObjectTypes type, SCIMDirections direction, HttpRequestTypes httpRequestType);
}
