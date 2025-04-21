using System;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore;

public interface IUpdateResourceV2
{
    /// <summary>
    /// Updates a resource asynchronously.
    /// </summary>
    /// <param name="patch">Patch request</param>
    /// <param name="appId">Application ID</param>
    /// <param name="correlationID">Correlation ID</param>
    /// <returns></returns>
    Task UpdateAsync(IPatch patch, string appId, string correlationID);
}
