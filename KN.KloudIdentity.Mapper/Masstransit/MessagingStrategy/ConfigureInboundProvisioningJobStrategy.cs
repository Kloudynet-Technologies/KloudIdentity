//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.BackgroundJobs;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Utils;
using System.Text.Json;

namespace KN.KloudIdentity.Mapper.Masstransit;

public class ConfigureInboundProvisioningJobStrategy(
    IJobManagementService jobManagementService,
    IKloudIdentityLogger logger
    ) : IMessageProcessorStrategy
{
    public async Task<IInterserviceResponseMsg> ProcessMessage(IInterserviceRequestMsg message, CancellationToken cancellationToken)
    {
        var inboundConfig = JsonSerializer.Deserialize<InboundJobSchedulerConfig>(message.Message);

        var errorResult = ValidateInboundJobSchedulerConfig(inboundConfig);

        if (errorResult != null)
        {
            _ = CreateLogAsync(inboundConfig!.AppId, errorResult);

            return errorResult;
        }

        await Task.Run(() => ConfigureJob(inboundConfig!));

        var response = new ConfigureInboundProvisioningJobResponse
        {
            Message = inboundConfig!.IsInboundJobEnabled? "Job added or updated successfully." : "Removed job successfully."
        };

        _ = CreateLogAsync(inboundConfig!.AppId, response);

        return response; 
    }

    private void ConfigureJob(InboundJobSchedulerConfig config)
    {
        if (config.IsInboundJobEnabled)
        {
            jobManagementService.AddOrUpdateJobAsync(config.AppId, config.InboundJobFrequency!);
        }
        else
        {
            jobManagementService.RemoveJob(config.AppId);
        }
    }

    private ConfigureInboundProvisioningJobResponse? ValidateInboundJobSchedulerConfig(InboundJobSchedulerConfig? config)
    {
        if (config == null)
        {
            return new ConfigureInboundProvisioningJobResponse
            {
                Message = string.Empty,
                IsError = true,
                ErrorMessage = "Deserialization failed: query is null."
            };
        }

        var validationResults = config.Validate().ToList();

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

    private async Task CreateLogAsync(string appId, ConfigureInboundProvisioningJobResponse response)
    {
        var logType = response.IsError == true ? LogType.InboundError.ToString() : LogType.InboundEdit.ToString();
        var logSeverity = response.IsError == true ? LogSeverities.Error : LogSeverities.Information;

        await logger.CreateLogAsync(new CreateLogEntity(
               appId,
               logType,
               logSeverity,
               "Configured Inbound Provisioning Jobs",
               response.Message,
               Guid.NewGuid().ToString(),
               AppConstant.LoggerName,
               DateTime.UtcNow,
               "System",
               null,
               null
               ));
    }

}
