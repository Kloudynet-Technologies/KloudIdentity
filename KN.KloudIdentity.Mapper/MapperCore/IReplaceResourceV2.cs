using System;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore;

public interface IReplaceResourceV2
{
    Task ReplaceAsync(Core2EnterpriseUser resource, string appId, string correlationID);
}
