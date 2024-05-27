//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore;

/// <summary>
/// Interface for creating a new resource.
/// </summary>
/// <typeparam name="T">The type of resource to create.</typeparam>
public interface ICreateResource<T> : IAPIMapperBase<T> where T : Resource
{
    /// <summary>
    /// Creates a new resource asynchronously.
    /// </summary>
    /// <param name="resource">The resource to create.</param>
    /// <param name="appId">The ID of the application.</param>
    /// <param name="correlationID">The correlation ID.</param>
    /// <returns>The created resource.</returns>
    Task<T> ExecuteAsync(T resource, string appId, string correlationID);
}
