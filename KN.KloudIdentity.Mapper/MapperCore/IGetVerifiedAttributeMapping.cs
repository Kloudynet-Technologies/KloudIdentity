using KN.KloudIdentity.Mapper.Config.Db;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore;

public interface IGetVerifiedAttributeMapping
{
    Task<JObject> GetVerifiedAsync(string appId, ObjectTypes type, int stepId);
}
