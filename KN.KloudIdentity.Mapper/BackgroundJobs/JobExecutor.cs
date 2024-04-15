using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.MapperCore.Inbound;
using KN.KloudIdentity.Mapper.Utils;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.BackgroundJobs;

public class JobExecutor : IJobExecutor
{
    private readonly IFetchInboundResources<JObject> _fetchInboundResources;
    private readonly ICreateResourceInbound<JObject> _createInboundResources;
    private readonly IKloudIdentityLogger _logger;
    public JobExecutor(IFetchInboundResources<JObject> fetchInboundResources,
        ICreateResourceInbound<JObject> createInboundResources,
        IKloudIdentityLogger logger)
    {
        _fetchInboundResources = fetchInboundResources;
        _createInboundResources = createInboundResources;
        _logger = logger;
    }
    public async Task ExecuteAsync(string jobId)
    {
        try
        {
            var correlationId = Guid.NewGuid().ToString();
            var users = await _fetchInboundResources.FetchInboundResourcesAsync(jobId, correlationId);

            await _createInboundResources.ExecuteAsync(users, jobId, correlationId);

            _= CreateLogAsync(jobId, correlationId);
        }
        catch (Exception ex)
        {
            throw new Exception("Error while executing the job", ex);
        }

    }

    private async Task CreateLogAsync(string appId, string correlationID)
    {
        var eventInfo = $"Execute cron job for appId (#{appId})";
        var logMessage = $"Executed cron job for inbound app #{appId}";

        var logEntity = new CreateLogEntity(
            appId,
            LogType.Edit.ToString(),
            LogSeverities.Information,
            eventInfo,
            logMessage,
            correlationID,
            AppConstant.LoggerName,
            DateTime.UtcNow,
            AppConstant.User,
            null,
            null
        );

        await _logger.CreateLogAsync(logEntity);
    }
}
