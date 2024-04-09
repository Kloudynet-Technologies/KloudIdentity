using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public interface ICreateInboundResources<T> where T : JObject
{
    Task CreateInboundResourcesAsync(IList<T> resources, string appId, string correlationId, CancellationToken cancellationToken = default);
}
