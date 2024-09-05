
//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using Hangfire;
using KN.KloudIdentity.Mapper.Domain;

namespace KN.KloudIdentity.Mapper.BackgroundJobs;

public class JobManagementService(IInboundJobExecutor jobExecutor) : IJobManagementService
{
    public void AddOrUpdateJobAsync(InboundAppConfig inboundAppConfig, string cronExpression)
    {
        RecurringJob.AddOrUpdate(inboundAppConfig.AppId, () => jobExecutor.ExecuteAsync(inboundAppConfig), cronExpression);

    }

    public void RemoveJob(string key)
    {
        RecurringJob.RemoveIfExists(key);
    }
}