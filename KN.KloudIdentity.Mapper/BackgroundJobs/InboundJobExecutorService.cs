//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Domain;

namespace KN.KloudIdentity.Mapper.BackgroundJobs;

public class InboundJobExecutorService : IInboundJobExecutor
{
    public async Task ExecuteAsync(InboundAppConfig inboundAppConfig)
    {
        await Task.CompletedTask;
    }
}
