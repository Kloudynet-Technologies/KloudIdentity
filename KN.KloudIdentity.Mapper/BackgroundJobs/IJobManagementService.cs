//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Domain;

namespace KN.KloudIdentity.Mapper.BackgroundJobs;

public interface IJobManagementService
{
    void AddOrUpdateJobAsync(InboundAppConfig inboundAppConfig, string cronExpression);
    void RemoveJob(string key);
}
