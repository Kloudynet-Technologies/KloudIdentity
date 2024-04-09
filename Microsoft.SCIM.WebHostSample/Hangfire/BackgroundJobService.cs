using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.MapperCore.Inbound;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace Microsoft.SCIM.WebHostSample.Hangfire;

public class BackgroundJobService : IBackgroundJobService
{
    private readonly IFetchInboundResources<JObject> _listUser;
    private readonly ICreateResourceInbound<JObject> _createUser;
    public BackgroundJobService(IFetchInboundResources<JObject>listUser, ICreateResourceInbound<JObject> createUser)
    {
        _listUser = listUser;
        _createUser = createUser;
    }
    public  Task RunSheduleJobAsybc()
    {
        _= ExecuteAsync();
      return Task.CompletedTask;
    }

    private async Task ExecuteAsync()
    {
        var appId = "scimapp02";
        var correlationId = Guid.NewGuid().ToString();
        var cancellationToken = new System.Threading.CancellationToken();
        var users = await _listUser.FetchInboundResourcesAsync(appId, correlationId, cancellationToken);
        await _createUser.ExecuteAsync(users, appId, correlationId);
    }
}
