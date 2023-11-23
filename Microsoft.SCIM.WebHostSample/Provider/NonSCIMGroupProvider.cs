//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Microsoft.SCIM.WebHostSample;

/// <summary>
/// Represents a provider for non-SCIM application user groups.
/// </summary>
public class NonSCIMGroupProvider : ProviderBase
{
    [Obsolete("Use CreateAsync(Resource, string, string) instead.")]
    public override Task<Resource> CreateAsync(Resource resource, string correlationIdentifier)
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
        throw new NotImplementedException();
    }

    public override Task DeleteAsync(IResourceIdentifier resourceIdentifier, string correlationIdentifier, string appId = null)
    {
        throw new NotImplementedException();
    }

    [Obsolete("Use ReplaceAsync(IResourceRetrievalParameters, string, string) instead.")]
    public override Task<Resource> ReplaceAsync(Resource resource, string correlationIdentifier)
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
    public override Task UpdateAsync(IPatch patch, string correlationIdentifier)
    {
        throw new NotImplementedException();
    }

    public override Task UpdateAsync(IPatch patch, string correlationIdentifier, string appId = null)
    {
        throw new NotImplementedException();
    }
}
