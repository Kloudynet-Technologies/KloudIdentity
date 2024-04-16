using Hangfire;
using KN.KloudIdentity.Mapper.MapperCore.Inbound;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.BackgroundJobs;

public class JobExecutor : IJobExecutor
{
    private readonly IFetchInboundResources<JObject> _fetchInboundResources;
    private readonly ICreateResourceInbound<JObject> _createInboundResources;
    public JobExecutor(IFetchInboundResources<JObject> fetchInboundResources,
        ICreateResourceInbound<JObject> createInboundResources)
    {
        _fetchInboundResources = fetchInboundResources;
        _createInboundResources = createInboundResources;
    }

    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync(string jobId)
    {
        var correlationId = Guid.NewGuid().ToString();
        var users = await _fetchInboundResources.FetchInboundResourcesAsync(jobId, correlationId);

        _ = _createInboundResources.ExecuteAsync(users, jobId, correlationId);

    }
}
