﻿//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore;

/// <summary>
/// Interface for deleting a resource.
/// </summary>
/// <typeparam name="T">The type of resource to delete.</typeparam>
[Obsolete("This interface is deprecated. Use IDeleteResourceV2 instead.")]
public interface IDeleteResource<T> : IAPIMapperBase<T> where T : Resource
{
    /// <summary>
    /// Deletes a resource asynchronously.
    /// </summary>
    /// <param name="resource">The resource to delete.</param>
    /// <param name="appId">The ID of the application.</param>
    /// <param name="correlationID">The correlation ID.</param>
    /// <returns>A task representing the asynchronous operation. The result of the task contains the deleted resource.</returns>
    [Obsolete("This method is deprecated. Use IDeleteResourceV2.DeleteAsync(IResourceIdentifier, string, string) instead.")]
    Task DeleteAsync(IResourceIdentifier resourceIdentifier, string appId, string correlationID);
}
