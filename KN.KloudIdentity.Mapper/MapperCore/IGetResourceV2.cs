using System;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore;

public interface IGetResourceV2
{
    Task<Core2EnterpriseUser> GetAsync(string identifier, string appId, string correlationID);
}
