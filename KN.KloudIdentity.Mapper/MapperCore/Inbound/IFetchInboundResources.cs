using Microsoft.SCIM;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.MapperCore.Inbound;

public interface IFetchInboundResources : IAPIMapperBaseInbound
{
    Task<JObject?> FetchInboundResourcesAsync(string appId, string correlationId, CancellationToken cancellationToken = default);
}
