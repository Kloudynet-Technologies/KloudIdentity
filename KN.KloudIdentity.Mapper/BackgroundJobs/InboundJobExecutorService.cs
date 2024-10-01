//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using KN.KloudIdentity.Mapper.MapperCore.Inbound;

namespace KN.KloudIdentity.Mapper.BackgroundJobs;

public class InboundJobExecutorService : IInboundJobExecutor
{
    private readonly IGetInboundAppConfigQuery _getInboundAppConfigQuery;
    private readonly IAuthContext _authContext;
    private readonly IFetchInboundResources _fetchInboundResources;
    public InboundJobExecutorService(
        IGetInboundAppConfigQuery getInboundAppConfigQuery, IAuthContext authContext,
        IFetchInboundResources fetchInboundResources
        )
    {
        _getInboundAppConfigQuery = getInboundAppConfigQuery;
        _authContext = authContext;
        _fetchInboundResources = fetchInboundResources;
    }
    public async Task ExecuteAsync(string appId)
     
    {
        await _fetchInboundResources.FetchInboundResourcesAsync(appId, Guid.NewGuid().ToString());
        var inboundConfig = await _getInboundAppConfigQuery.GetAsync(appId);

        var toekn = _authContext.GetTokenAsync(inboundConfig, SCIMDirections.Inbound);

        await Task.CompletedTask;
    }
}
