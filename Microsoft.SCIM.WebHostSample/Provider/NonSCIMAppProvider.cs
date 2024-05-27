//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Microsoft.SCIM.WebHostSample;

/// <summary>
/// Provider for non-SCIM applications.
/// </summary>
public class NonSCIMAppProvider : ProviderBase
{
    private readonly ProviderBase _groupProvider;
    private readonly ProviderBase _userProvider;

    public NonSCIMAppProvider(
        NonSCIMUserProvider nonSCIMUserProvider,
        NonSCIMGroupProvider nonSCIMGroupProvider
    )
    {
        _userProvider = nonSCIMUserProvider;
        _groupProvider = nonSCIMGroupProvider;
    }

    /// <summary>
    /// Creates a new resource asynchronously.
    /// </summary>
    /// <param name="resource">The resource to create.</param>
    /// <param name="correlationIdentifier">The correlation identifier.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the created resource.</returns>
    [Obsolete("Use CreateAsync(Resource, string, string) instead.")]
    public override Task<Resource> CreateAsync(Resource resource, string correlationIdentifier)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Initiates the asynchronous deletion of a resource based on its schema identifier.
    /// </summary>
    /// <param name="resourceIdentifier">The identifier of the resource to be deleted.</param>
    /// <param name="correlationIdentifier">The correlation identifier associated with the operation.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    [Obsolete("Use DeleteAsync(IResourceIdentifier, string, string) instead.")]
    public override async Task DeleteAsync(
        IResourceIdentifier resourceIdentifier,
        string correlationIdentifier
    )
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Retrieves a resource based on the given parameters and correlation identifier.
    /// </summary>
    /// <param name="parameters">The parameters used to retrieve the resource.</param>
    /// <param name="correlationIdentifier">The correlation identifier associated with the request.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved resource.</returns>
    public override Task<Resource> RetrieveAsync(
        IResourceRetrievalParameters parameters,
        string correlationIdentifier
    )
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Updates a resource asynchronously.
    /// This method is obsolete. Use UpdateAsync(IPatch, string, string) instead.
    /// </summary>
    /// <param name="patch"></param>
    /// <param name="correlationIdentifier"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    [Obsolete("Use UpdateAsync(IPatch, string, string) instead.")]
    public override Task UpdateAsync(IPatch patch, string correlationIdentifier)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Queries the Non-SCIM application provider for resources based on the given query parameters.
    /// </summary>
    /// <param name="parameters">The query parameters.</param>
    /// <param name="correlationIdentifier">The correlation identifier.</param>
    /// <returns>An array of resources.</returns>
    public override Task<Resource[]> QueryAsync(
        IQueryParameters parameters,
        string correlationIdentifier
    )
    {
        if (parameters.SchemaIdentifier.Equals(SchemaIdentifiers.Core2EnterpriseUser))
        {
            return _userProvider.QueryAsync(parameters, correlationIdentifier);
        }

        if (parameters.SchemaIdentifier.Equals(SchemaIdentifiers.Core2Group))
        {
            return _groupProvider.QueryAsync(parameters, correlationIdentifier);
        }

        throw new NotImplementedException();
    }

    /// <summary>
    /// Replaces a new resource asynchronously.
    /// </summary>
    /// <param name="resource">The resource to replace.</param>
    /// <param name="correlationIdentifier">The correlation identifier.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the replaced  resource.</returns>
    /// <exception cref="NotImplementedException"></exception>
    [Obsolete("Use ReplaceAsync(Resource, string, string) instead.")]
    public override Task<Resource> ReplaceAsync(Resource resource, string correlationIdentifier)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Updates a resource asynchronously.
    /// </summary>
    /// <param name="resource"></param>
    /// <param name="correlationIdentifier"></param>
    /// <param name="appId"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public override Task<Resource> ReplaceAsync(Resource resource, string correlationIdentifier, string appId = null)
    {
        if (resource is Core2EnterpriseUser)
        {
            return _userProvider.ReplaceAsync(resource, correlationIdentifier, appId);
        }

        if (resource is Core2Group)
        {
            return _groupProvider.ReplaceAsync(resource, correlationIdentifier, appId);
        }

        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates a new resource asynchronously.
    /// </summary>
    /// <param name="resource"></param>
    /// <param name="correlationIdentifier"></param>
    /// <param name="appId"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public override Task<Resource> CreateAsync(Resource resource, string correlationIdentifier, string appId = null)
    {
        if (resource is Core2EnterpriseUser)
        {
            return _userProvider.CreateAsync(resource, correlationIdentifier, appId);
        }

        if (resource is Core2Group)
        {
            return _groupProvider.CreateAsync(resource, correlationIdentifier, appId);
        }

        throw new NotImplementedException();
    }

    /// <summary>
    /// Deletes a resource asynchronously.
    /// </summary>
    /// <param name="resourceIdentifier"></param>
    /// <param name="correlationIdentifier"></param>
    /// <param name="appId"></param>
    /// <returns></returns>
    public override async Task DeleteAsync(IResourceIdentifier resourceIdentifier, string correlationIdentifier, string appId = null)
    {
        if (resourceIdentifier.SchemaIdentifier.Equals(SchemaIdentifiers.Core2EnterpriseUser))
        {
            await _userProvider.DeleteAsync(resourceIdentifier, correlationIdentifier, appId);
        }

        if (resourceIdentifier.SchemaIdentifier.Equals(SchemaIdentifiers.Core2Group))
        {
            await _groupProvider.DeleteAsync(resourceIdentifier, correlationIdentifier, appId);
        }
    }

    /// <summary>
    /// Updates a resource asynchronously.
    /// </summary>
    /// <param name="patch"></param>
    /// <param name="correlationIdentifier"></param>
    /// <param name="appId"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public override async Task UpdateAsync(IPatch patch, string correlationIdentifier, string appId = null)
    {
        if (patch == null)
        {
            throw new ArgumentNullException(nameof(patch));
        }

        if (string.IsNullOrWhiteSpace(patch.ResourceIdentifier.Identifier))
        {
            throw new ArgumentException(nameof(patch));
        }

        if (string.IsNullOrWhiteSpace(patch.ResourceIdentifier.SchemaIdentifier))
        {
            throw new ArgumentException(nameof(patch));
        }

        if (patch.ResourceIdentifier.SchemaIdentifier.Equals(SchemaIdentifiers.Core2EnterpriseUser))
        {
            await _userProvider.UpdateAsync(patch, correlationIdentifier, appId);
        }

        if (patch.ResourceIdentifier.SchemaIdentifier.Equals(SchemaIdentifiers.Core2Group))
        {
            await _groupProvider.UpdateAsync(patch, correlationIdentifier, appId);
        }
    }

    public override Task<Resource> RetrieveAsync(IResourceRetrievalParameters parameters, string correlationIdentifier, string appId = null)
    {
        if (parameters.SchemaIdentifier.Equals(SchemaIdentifiers.Core2EnterpriseUser))
        {
            return _userProvider.RetrieveAsync(parameters, correlationIdentifier, appId);
        }

        if (parameters.SchemaIdentifier.Equals(SchemaIdentifiers.Core2Group))
        {
            return _groupProvider.RetrieveAsync(parameters, correlationIdentifier, appId);
        }

        throw new NotImplementedException();
    }
}
