using Hangfire;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KN.KloudIdentity.Mapper.BackgroundJobs;

public class JobCreationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public JobCreationService(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private async Task CreateJobsAsybc()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var listApplicationsQuery = scope.ServiceProvider.GetRequiredService<IListApplicationsQuery>();
            var jobExecutor = scope.ServiceProvider.GetRequiredService<IJobExecutor>();

            var applications = await listApplicationsQuery.ListAsync();
            var cronJobs = new Dictionary<string, string>();
            foreach (var app in applications)
            {
                cronJobs.Add(app.AppId, "0 12 * * MON");
            }

            foreach (var cronJob in cronJobs)
            {
                string jobId = cronJob.Key;
                string cronExpression = cronJob.Value;
                RecurringJob.AddOrUpdate(jobId, () => jobExecutor.ExecuteAsync(jobId), cronExpression);
            } 
        }
       
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await CreateJobsAsybc();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
