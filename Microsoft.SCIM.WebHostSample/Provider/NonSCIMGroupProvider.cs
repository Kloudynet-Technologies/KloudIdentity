//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.MapperCore;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;

namespace Microsoft.SCIM.WebHostSample;

/// <summary>
/// Represents a provider for non-SCIM application user groups.
/// </summary>
public class NonSCIMGroupProvider : ProviderBase
{
    private readonly ICreateResource<Core2Group> _createGroup;
    private readonly IDeleteResource<Core2Group> _deleteGroup;
    private readonly IReplaceResource<Core2Group> _replaceGroup;
    private readonly IUpdateResource<Core2Group> _updateGroup;

    private readonly IGetResource<Core2Group> _getGroup;

    /// <summary>
    /// Constructor that initializes the NonSCIMGroupProvider with resource creation, deletion, and replacement services.
    /// </summary>
    /// <param name="createGroup">Service for creating resources of type Core2Group.</param>
    /// <param name="deleteGroup">Service for deleting resources of type Core2Group.</param>
    /// <param name="replaceGroup">Service for replacing resources of type Core2Group.</param>
    /// <param name="updateGroup">Service for updating resources of type Core2Group.</param>
    public NonSCIMGroupProvider(
        ICreateResource<Core2Group> createGroup,
        IDeleteResource<Core2Group> deleteGroup,
        IReplaceResource<Core2Group> replaceGroup,
        IUpdateResource<Core2Group> updateGroup,
        IGetResource<Core2Group> getGroup
    )
    {
        _createGroup = createGroup;
        _deleteGroup = deleteGroup;
        _replaceGroup = replaceGroup;
        _updateGroup = updateGroup;
        _getGroup = getGroup;
    }

    /// <summary>
    /// Creates a new group resource asynchronously.
    /// </summary>
    /// <param name="resource">The group resource to be created.</param>
    /// <param name="correlationIdentifier">A correlation identifier for tracking the operation.</param>
    /// <param name="appId">The application ID associated with the group.</param>
    /// <returns>The newly created group resource.</returns>
    public override async Task<Resource> CreateAsync(Resource resource, string correlationIdentifier, string appId = null)
    {
        // Validation: Ensure the resource doesn't already have an identifier
        if (resource.Identifier != null)
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        Core2Group group = resource as Core2Group;

        // Validation: Ensure the group has a non-empty display name
        if (string.IsNullOrWhiteSpace(group.DisplayName))
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        // Update Metadata
        DateTime created = DateTime.UtcNow;
        group.Metadata.Created = created;
        group.Metadata.LastModified = created;

        // Generate a unique identifier for the resource
        string resourceIdentifier = Guid.NewGuid().ToString();
        resource.Identifier = resourceIdentifier;

        // Execute the creation service and return the created group
        var createdGroup = await _createGroup.ExecuteAsync(group, appId, correlationIdentifier);
        return createdGroup;
    }

    [Obsolete("Use CreateAsync(Resource, string, string) instead.")]
    public override Task<Resource> CreateAsync(Resource resource, string correlationIdentifier)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Deletes a group resource asynchronously.
    /// </summary>
    /// <param name="resourceIdentifier">The identifier of the group resource to be deleted.</param>
    /// <param name="correlationIdentifier">A correlation identifier for tracking the operation.</param>
    /// <param name="appId">The application ID associated with the group.</param>
    /// <exception cref="HttpResponseException">Thrown if the resource identifier is null or empty.</exception>
    public override async Task DeleteAsync(IResourceIdentifier resourceIdentifier, string correlationIdentifier, string appId = null)
    {
        // Validation: Ensure the resource identifier is not null or empty
        if (string.IsNullOrWhiteSpace(resourceIdentifier?.Identifier))
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        // Execute the deletion service
        await _deleteGroup.DeleteAsync(resourceIdentifier, appId, correlationIdentifier);
    }

    [Obsolete("Use DeleteAsync(IResourceIdentifier, string, string) instead.")]
    public override Task DeleteAsync(IResourceIdentifier resourceIdentifier, string correlationIdentifier)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Queries group resources based on specified parameters.
    /// </summary>
    /// <param name="parameters">The query parameters.</param>
    /// <param name="correlationIdentifier">A correlation identifier for tracking the operation.</param>
    /// <returns>An array of group resources matching the query parameters.</returns>
    public override Task<Resource[]> QueryAsync(IQueryParameters parameters, string correlationIdentifier)
    {
        /*
         * @TODO: Implement this method.
         * Perform logic to query and return groups based on the specified parameters.
         */
        var groups = new List<Resource>();
        return Task.FromResult(groups.ToArray());
    }

    /// <summary>
    /// Replaces a group resource asynchronously.
    /// </summary>
    /// <param name="resource">The group resource to be replaced.</param>
    /// <param name="correlationIdentifier">A correlation identifier for tracking the operation.</param>
    /// <param name="appId">The application ID associated with the group.</param>
    /// <returns>The replaced group resource.</returns>
    /// <exception cref="HttpResponseException">Thrown if the resource identifier is null or the display name is empty.</exception>
    public override async Task<Resource> ReplaceAsync(Resource resource, string correlationIdentifier, string appId = null)
    {
        // Validation: Ensure the resource has an identifier
        if (resource.Identifier == null)
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        Core2Group group = resource as Core2Group;

        // Validation: Ensure the group has a non-empty display name
        if (string.IsNullOrWhiteSpace(group.DisplayName))
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        // Update the last modified timestamp
        group.Metadata.LastModified = DateTime.UtcNow;

        return await _replaceGroup.ReplaceAsync(group, appId, correlationIdentifier);
    }

    [Obsolete("Use ReplaceAsync(Resource, string, string) instead.")]
    public override Task<Resource> ReplaceAsync(Resource resource, string correlationIdentifier)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Retrieves a group resource asynchronously based on retrieval parameters.
    /// </summary>
    /// <param name="parameters">The retrieval parameters.</param>
    /// <param name="correlationIdentifier">A correlation identifier for tracking the operation.</param>
    /// <returns>The retrieved group resource.</returns>
    /// <exception cref="NotImplementedException">Thrown since retrieval is not implemented.</exception>
    [Obsolete("Use RetrieveAsync(IResourceRetrievalParameters, string, string) instead.")]
    public override async Task<Resource> RetrieveAsync(IResourceRetrievalParameters parameters, string correlationIdentifier)
    {
        throw new HttpResponseException(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Partially updates members of a group resource asynchronously.
    /// </summary>
    /// <param name="patch">The patch operation to be applied to the group resource.</param>
    /// <param name="correlationIdentifier">A correlation identifier for tracking the operation.</param>
    /// <param name="appId">The application ID associated with the group.</param>
    public override async Task UpdateAsync(IPatch patch, string correlationIdentifier, string appId = null)
    {
        if (string.IsNullOrWhiteSpace(patch?.ResourceIdentifier?.Identifier))
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        await _updateGroup.UpdateAsync(patch, appId, correlationIdentifier);
    }

    /// <summary>
    /// Gets the group resource asynchronously based on the given parameters.
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="correlationIdentifier"></param>
    /// <param name="appId"></param>
    /// <returns></returns>
    /// <exception cref="HttpResponseException"></exception>
    public async override Task<Resource> RetrieveAsync(IResourceRetrievalParameters parameters, string correlationIdentifier, string appId = null)
    {
        if (string.IsNullOrWhiteSpace(parameters?.ResourceIdentifier?.Identifier))
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        return await _getGroup.GetAsync(parameters.ResourceIdentifier.Identifier, appId, correlationIdentifier);
    }

    [Obsolete("Use UpdateAsync(IResourceRetrievalParameters, string, string) instead.")]
    public override Task UpdateAsync(IPatch patch, string correlationIdentifier)
    {
        throw new NotImplementedException();
    }
}

