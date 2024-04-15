using System.Threading.Tasks;

namespace Microsoft.SCIM.WebHostSample.Hangfire;

public interface IBackgroundJobService
{
    Task RunSheduleJobAsybc();
}
