
//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using Hangfire;

namespace KN.KloudIdentity.Mapper.BackgroundJobs;

public class JobManagementService(IInboundJobExecutor jobExecutor) : IJobManagementService
{
    public void AddOrUpdateJobAsync(string appId, string cronExpression)
    {
        RecurringJob.AddOrUpdate(appId, () => jobExecutor.ExecuteAsync(appId), cronExpression);

    }

    public void RemoveJob(string key)
    {
        RecurringJob.RemoveIfExists(key);
    }
}