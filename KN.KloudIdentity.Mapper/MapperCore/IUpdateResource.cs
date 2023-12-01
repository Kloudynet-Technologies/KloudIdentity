//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore;

/// <summary>
/// Interface for updating a resource asynchronously.
/// </summary>
/// <typeparam name="T">The type of resource to update.</typeparam>
public interface IUpdateResource<T> : IAPIMapperBase<T> where T : Resource
{
    /// <summary>
    /// Updates a resource asynchronously.
    /// </summary>
    /// <param name="patch">The resource to update.</param>
    /// <param name="appId">The ID of the application.</param>
    /// <param name="correlationID">The correlation ID.</param>
    /// <returns></returns>
    Task UpdateAsync(IPatch patch, string appId, string correlationID);
}
