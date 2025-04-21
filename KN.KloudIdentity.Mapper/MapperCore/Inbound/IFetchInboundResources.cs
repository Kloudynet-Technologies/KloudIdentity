using KN.KloudIdentity.Mapper.Domain.Inbound;
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
    Task<JObject?> FetchInboundResourcesAsync(InboundConfig inboundConfig, string correlationId, CancellationToken cancellationToken = default);
}
