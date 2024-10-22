using System;
using Microsoft.SCIM;

namespace KN.KloudIdentity.Mapper.MapperCore;

public interface IDeleteResourceV2
{
    /// <summary>
    /// Deletes a resource asynchronously.
    /// </summary>
    /// <param name="resource">The resource to delete.</param>
    /// <param name="appId">The ID of the application.</param>
    /// <param name="correlationID">The correlation ID.</param>
    /// <returns>A task representing the asynchronous operation. The result of the task contains the deleted resource.</returns>
    Task DeleteAsync(IResourceIdentifier resourceIdentifier, string appId, string correlationID);
}
