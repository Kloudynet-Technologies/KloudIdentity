using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.BackgroundJobs;

public interface IJobCreationService : IHostedService
{
    Task CreateJobsAsybc();
}
