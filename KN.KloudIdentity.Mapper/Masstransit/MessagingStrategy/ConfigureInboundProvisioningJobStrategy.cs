//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.BackgroundJobs;
using KN.KloudIdentity.Mapper.Domain;
using System.Text.Json;

namespace KN.KloudIdentity.Mapper.Masstransit;

public class ConfigureInboundProvisioningJobStrategy(IJobManagementService jobManagementService) : IMessageProcessorStrategy
{
    public async Task<IInterserviceResponseMsg> ProcessMessage(IInterserviceRequestMsg message, CancellationToken cancellationToken)
    {
        var inboundConfig = JsonSerializer.Deserialize<InboundAppConfig>(message.Message);

        var errorResult = ValidateInboundAppConfig(inboundConfig);

        if (errorResult != null)
            return errorResult;

        await Task.Run(() => ConfigureJob(inboundConfig!));

        return new ConfigureInboundProvisioningJobResponse
        {
            Message = "Job added or updated successfully."
        };
    }

    private void ConfigureJob(InboundAppConfig config)
    {
        if (config.IsInboundJobEnabled)
        {
            jobManagementService.AddOrUpdateJobAsync(config, config.InboundJobScheduler!.InboundJobFrequency);
        }
        else
        {
            jobManagementService.RemoveJob(config.AppId);
        }
    }

    private ConfigureInboundProvisioningJobResponse? ValidateInboundAppConfig(InboundAppConfig? inboundAppConfig)
    {
        if (inboundAppConfig == null)
        {
            return new ConfigureInboundProvisioningJobResponse
            {
                Message = string.Empty,
                IsError = true,
                ErrorMessage = "Deserialization failed: query is null."
            };
        }

        var validationResults = inboundAppConfig.Validate().ToList();

        if (validationResults.Any())
        {
            var errorMessage = string.Join("; ", validationResults.Select(vr => vr.ErrorMessage));
            return new ConfigureInboundProvisioningJobResponse
            {
                Message = string.Empty,
                IsError = true,
                ErrorMessage = errorMessage
            };
        }

        return null;
    }

}
