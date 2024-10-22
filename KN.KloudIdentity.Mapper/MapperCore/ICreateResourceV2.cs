using System;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore;

public interface ICreateResourceV2
{
    Task<Core2EnterpriseUser> ExecuteAsync(Core2EnterpriseUser resource, string appId, string correlationID);
}
