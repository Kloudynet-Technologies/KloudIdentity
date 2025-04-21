//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.BackgroundJobs;
using KN.KloudIdentity.Mapper.MapperCore;

namespace KN.KloudIdentity.Mapper.Masstransit;

public class MessageProcessingFactory(
    IGetVerifiedAttributeMapping getVerifiedAttributeMapping,
    IJobManagementService jobManagementService,
    IKloudIdentityLogger logger
    )
{
    public IMessageProcessorStrategy CreateProcessor(string action)
    {
        return action switch
        {
            "GetVerifyMapping" => new GetVerifiedAttributeMappingStrategy(getVerifiedAttributeMapping),
            "ConfigureInboundProvisioningJob" => new ConfigureInboundProvisioningJobStrategy(jobManagementService, logger),
            _ => throw new InvalidOperationException($"Action {action} is not supported")
        };
    }
}
