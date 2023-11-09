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

    public NonSCIMAppProvider(NonSCIMUserProvider nonSCIMUserProvider, NonSCIMGroupProvider nonSCIMGroupProvider)
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
    public override Task<Resource> CreateAsync(Resource resource, string correlationIdentifier)
    {
        if (resource is Core2EnterpriseUser)
        {
            return _userProvider.CreateAsync(resource, correlationIdentifier);
        }

        if (resource is Core2Group)
        {
            return _groupProvider.CreateAsync(resource, correlationIdentifier);
        }

        throw new NotImplementedException();
    }

    public override Task DeleteAsync(IResourceIdentifier resourceIdentifier, string correlationIdentifier)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Retrieves a resource based on the given parameters and correlation identifier.
    /// </summary>
    /// <param name="parameters">The parameters used to retrieve the resource.</param>
    /// <param name="correlationIdentifier">The correlation identifier associated with the request.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved resource.</returns>
    public override Task<Resource> RetrieveAsync(IResourceRetrievalParameters parameters, string correlationIdentifier)
    {
        if (parameters.SchemaIdentifier.Equals(SchemaIdentifiers.Core2EnterpriseUser))
        {
            return _userProvider.RetrieveAsync(parameters, correlationIdentifier);
        }

        if (parameters.SchemaIdentifier.Equals(SchemaIdentifiers.Core2Group))
        {
            return _groupProvider.RetrieveAsync(parameters, correlationIdentifier);
        }

        throw new NotImplementedException();
    }

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
    public override Task<Resource[]> QueryAsync(IQueryParameters parameters, string correlationIdentifier)
    {
        if (parameters.SchemaIdentifier.Equals(SchemaIdentifiers.Core2EnterpriseUser))
        {
            return _userProvider.QueryAsync(parameters, correlationIdentifier);
        }

        if (parameters.SchemaIdentifier.Equals(SchemaIdentifiers.Core2Group))
        {
            return _userProvider.QueryAsync(parameters, correlationIdentifier);
        }

        throw new NotImplementedException();
    }
}
