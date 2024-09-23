//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

namespace KN.KloudIdentity.Mapper.BackgroundJobs;

public class InboundJobExecutorService : IInboundJobExecutor
{
    public async Task ExecuteAsync(string appId)
    {
        await Task.CompletedTask;
    }
}
