//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore;

/// <summary>
/// Interface to retrieving a resource by identifier.
/// </summary>
/// <typeparam name="T">The type of resource to map.</typeparam>
public interface IGetResource<T> : IAPIMapperBase<T> where T : Resource
{
    /// <summary>
    /// Retrieves a resource by identifier.
    /// </summary>
    /// <param name="identifier">The identifier of the resource to retrieve.</param>
    /// <param name="appId">The ID of the application making the request.</param>
    /// <param name="correlationID">The correlation ID for the request.</param>
    /// <returns>The resource object.</returns>
    Task<T> GetAsync(string identifier, string appId, string correlationID);
}
