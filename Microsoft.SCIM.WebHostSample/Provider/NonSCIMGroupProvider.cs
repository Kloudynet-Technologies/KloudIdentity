//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.MapperCore.User;
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

    /// <summary>
    /// Constructor that initializes the NonSCIMGroupProvider with a resource creation service.
    /// </summary>
    /// <param name="createGroup">Service for creating resources of type Core2Group.</param>
    /// <param name="deleteGroup">Service for deleting resources of type Core2Group.</param>
    public NonSCIMGroupProvider(ICreateResource<Core2Group> createGroup, IDeleteResource<Core2Group> deleteGroup, IReplaceResource<Core2Group> replaceGroup)
    {
        _createGroup = createGroup;
        _deleteGroup = deleteGroup;
        _replaceGroup = replaceGroup;
    }

    /// <summary>
    /// Asynchronously creates a new group, updates metadata, assigns an identifier, and invokes the creation service.
    /// </summary>
    /// <param name="resource">The resource to be created.</param>
    /// <param name="correlationIdentifier">Correlation identifier for tracking.</param>
    /// <returns>The created resource.</returns>
    /// <exception cref="HttpResponseException">Thrown for invalid input conditions.</exception>
    public override async Task<Resource> CreateAsync(Resource resource, string correlationIdentifier)
    {
        // Input validation: If the resource already has an identifier, reject the request.
        if (resource.Identifier != null)
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        Core2Group group = resource as Core2Group;

        // Input validation: Ensure that the group has a non-empty display name.
        if (string.IsNullOrWhiteSpace(group.DisplayName))
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        // Update Metadata
        DateTime created = DateTime.UtcNow;
        group.Metadata.Created = created;
        group.Metadata.LastModified = created;

        string resourceIdentifier = Guid.NewGuid().ToString();
        resource.Identifier = resourceIdentifier;

        // Invoke the createGroup service to create the group asynchronously.
        return await _createGroup.ExecuteAsync(group, "App-002", correlationIdentifier);
    }

    /// <summary>
    /// Not implemented: Deletes a resource identified by the given parameters.
    /// </summary>
    /// <param name="resourceIdentifier">Identifier of the resource to be deleted.</param>
    /// <param name="correlationIdentifier">Correlation identifier for tracking.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="NotImplementedException">Thrown to indicate the method is not implemented.</exception>
    public override async Task DeleteAsync(IResourceIdentifier resourceIdentifier, string correlationIdentifier)
    {
        if (string.IsNullOrWhiteSpace(resourceIdentifier?.Identifier))
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        await _deleteGroup.DeleteAsync(resourceIdentifier, "App-001", correlationIdentifier);
    }

    /// <summary>
    /// Retrieves an empty array of resources, indicating that querying is not supported.
    /// </summary>
    /// <param name="parameters">Query parameters.</param>
    /// <param name="correlationIdentifier">Correlation identifier for tracking.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public override Task<Resource[]> QueryAsync(IQueryParameters parameters, string correlationIdentifier)
    {
        /*
        * @TODO: Implement this method.
        */
        var groups = new List<Resource>();
        return Task.FromResult(groups.ToArray());
    }

    /// <summary>
    /// Not implemented: Replaces a resource with the provided one.
    /// </summary>
    /// <param name="resource">The resource to be replaced.</param>
    /// <param name="correlationIdentifier">Correlation identifier for tracking.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="NotImplementedException">Thrown to indicate the method is not implemented.</exception>
    public override async Task<Resource> ReplaceAsync(Resource resource, string correlationIdentifier)
    {
        if (resource.Identifier == null)
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        Core2Group group = resource as Core2Group;

        if (string.IsNullOrWhiteSpace(group.DisplayName))
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        // Update metadata
        group.Metadata.LastModified = DateTime.UtcNow;

        var res = await _replaceGroup.ReplaceAsync(group, "App-002", correlationIdentifier);

        return res;
    }

    /// <summary>
    /// Retrieves a resource asynchronously based on the provided retrieval parameters.
    /// </summary>
    /// <param name="parameters">Retrieval parameters.</param>
    /// <param name="correlationIdentifier">Correlation identifier for tracking.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown for null parameters.</exception>
    /// <exception cref="HttpResponseException">Thrown if the resource is not found.</exception>
    public override Task<Resource> RetrieveAsync(IResourceRetrievalParameters parameters, string correlationIdentifier)
    {
        // Input validation: Ensure parameters and correlationIdentifier are not null or empty.
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        if (string.IsNullOrWhiteSpace(correlationIdentifier))
        {
            throw new ArgumentNullException(nameof(correlationIdentifier));
        }

        if (string.IsNullOrEmpty(parameters?.ResourceIdentifier?.Identifier))
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        // Attempt to retrieve the resource based on the identifier.
        string identifier = parameters.ResourceIdentifier.Identifier;

        // Not implemented: Retrieve the group from storage. Currently, this block is commented out.
        // if (this.storage.Groups.ContainsKey(identifier))
        // {
        //     if (this.storage.Groups.TryGetValue(identifier, out Core2Group group))
        //     {
        //         Resource result = group as Resource;
        //         return Task.FromResult(result);
        //     }
        // }

        // Throw HttpResponseException if the resource is not found.
        throw new HttpResponseException(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Not implemented: Updates a resource with the provided patch.
    /// </summary>
    /// <param name="patch">The patch to be applied to the resource.</param>
    /// <param name="correlationIdentifier">Correlation identifier for tracking.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    /// <exception cref="NotImplementedException">Thrown to indicate the method is not implemented.</exception>
    public override Task UpdateAsync(IPatch patch, string correlationIdentifier)
    {
        throw new NotImplementedException();
    }
}
