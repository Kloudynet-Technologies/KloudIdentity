using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices.JavaScript;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public interface ICreateResourceInbound<T> : IAPIMapperBaseInbound<T> where T : JObject
{
    Task ExecuteAsync(IList<JObject> resources, string appId, string correlationID);
}
