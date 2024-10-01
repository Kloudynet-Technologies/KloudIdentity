using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices.JavaScript;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public interface ICreateResourceInbound : IAPIMapperBaseInbound
{
    Task ExecuteAsync(string appId);
}
