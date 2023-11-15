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
    public override Task<Resource> CreateAsync(Resource resource, string correlationIdentifier)
    {
        throw new NotImplementedException();
    }

    public override Task DeleteAsync(
        IResourceIdentifier resourceIdentifier,
        string correlationIdentifier
    )
    {
        throw new NotImplementedException();
    }

    public override Task<Resource> ReplaceAsync(Resource resource, string correlationIdentifier)
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

    public override Task UpdateAsync(IPatch patch, string correlationIdentifier)
    {
        throw new NotImplementedException();
    }
}
