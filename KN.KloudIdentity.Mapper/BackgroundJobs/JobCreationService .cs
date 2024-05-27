using Hangfire;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KN.KloudIdentity.Mapper.BackgroundJobs;

public class JobCreationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private static HangfireOptions _hangfireOptions;

    public JobCreationService(
        IServiceProvider serviceProvider,
       HangfireOptions hangfireOptions)
    {
        _serviceProvider = serviceProvider;
        _hangfireOptions = hangfireOptions;
    }
    private async Task CreateJobsAsync()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var listApplicationsQuery = scope.ServiceProvider.GetRequiredService<IListApplicationsQuery>();
            var jobExecutor = scope.ServiceProvider.GetRequiredService<IJobExecutor>();

            var applications = await listApplicationsQuery.ListAsync();

            foreach (var app in applications)
            {
                if (app.IsEnabled)
                {
                    string jobId = app.AppId;
                    string cronExpression = _hangfireOptions.RecurringJobCronExpression;
                    RecurringJob.AddOrUpdate(jobId, () => jobExecutor.ExecuteAsync(jobId), cronExpression);
                }
            }
        }
    }

    private void RemoveJobs(string key)
    {
        RecurringJob.RemoveIfExists(key);
    }

    public void RemoveDisableJobsAndAddEnableIfNotExist()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var listApplicationsQuery = scope.ServiceProvider.GetRequiredService<IListApplicationsQuery>();
            var jobExecutor = scope.ServiceProvider.GetRequiredService<IJobExecutor>();

            var applications = listApplicationsQuery.ListAsync().Result;

            foreach (var app in applications)
            {
                if (!app.IsEnabled)
                {
                    RemoveJobs(app.AppId);
                }
                else
                {
                    _ = CreateJobsAsync();
                }
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {

        await CreateJobsAsync();

        RecurringJob.AddOrUpdate("CleanupDisabledAndAddNewJobs", () => RemoveDisableJobsAndAddEnableIfNotExist(), _hangfireOptions.RemoveJobCronExpression);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
