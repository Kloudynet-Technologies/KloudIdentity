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
    [Obsolete("Use CreateAsync(Resource, string, string) instead.")]
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
        throw new NotImplementedException();
    }

    public override Task<Resource> CreateAsync(Resource resource, string correlationIdentifier, string appId = null)
    {
        throw new NotImplementedException();
    }

    [Obsolete("Use DeleteAsync(IResourceIdentifier, string, string) instead.")]
    public override Task DeleteAsync(
        IResourceIdentifier resourceIdentifier,
        string correlationIdentifier
    )
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
    public override Task DeleteAsync(IResourceIdentifier resourceIdentifier, string correlationIdentifier, string appId = null)
    {
        throw new NotImplementedException();
    }

    [Obsolete("Use ReplaceAsync(IResourceRetrievalParameters, string, string) instead.")]
    public override async Task<Resource> ReplaceAsync(Resource resource, string correlationIdentifier)
    {
        throw new NotImplementedException();
    }

    public override Task<Resource> ReplaceAsync(Resource resource, string correlationIdentifier, string appId = null)
    {
        throw new NotImplementedException();
    }

    public override Task<Resource> RetrieveAsync(
        IResourceRetrievalParameters parameters,
        string correlationIdentifier
    )
    {
        throw new NotImplementedException();
    }

    [Obsolete("Use UpdateAsync(IResourceRetrievalParameters, string, string) instead.")]
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

    public override Task UpdateAsync(IPatch patch, string correlationIdentifier, string appId = null)
    {
        throw new NotImplementedException();
    }
}
