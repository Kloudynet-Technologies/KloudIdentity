//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.MapperCore.Inbound;

namespace KN.KloudIdentity.Mapper.BackgroundJobs;

public class InboundJobExecutorService : IInboundJobExecutor
{
    private readonly ICreateResourceInbound _createResourceInbound;
    public InboundJobExecutorService(ICreateResourceInbound createResourceInbound)
    {
        _createResourceInbound = createResourceInbound;
    }

    public async Task ExecuteAsync(string appId)
    {
        await _createResourceInbound.ExecuteAsync(appId);
    }
}
