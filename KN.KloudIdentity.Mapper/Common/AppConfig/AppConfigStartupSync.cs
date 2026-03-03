using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Serilog;

namespace KN.KloudIdentity.Mapper.Common.AppConfig;

public class AppConfigStartupSync(
    IServiceScopeFactory serviceScopeFactory
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var addOrEditAppConfig = scope.ServiceProvider.GetRequiredService<IAddOrEditAppConfig>();
        var listApplicationConfigsQuery = scope.ServiceProvider.GetRequiredService<IListApplicationConfigsQuery>();
        const int maxAttempts = 10;

        // Small delay helps when SCIM starts before RabbitMQ/MgtPortal is ready
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

        for (int attempt = 1; attempt <= maxAttempts && !cancellationToken.IsCancellationRequested; attempt++)
        {
            try
            {
                var appConfigs = await listApplicationConfigsQuery.ListAsync(cancellationToken);

                foreach (var appConfig in appConfigs)
                {
                    try
                    {
                        var json = JsonConvert.SerializeObject(appConfig, new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Ignore,
                            Formatting = Formatting.None
                        });
                        var update = new AppConfigSnapshotUpdated
                        {
                            AppId = appConfig.AppId,
                            GeneratedAtUtc = DateTime.UtcNow,
                            Message = json,
                            PerformedBy = "system-startup-sync"
                        };
                        await addOrEditAppConfig.AddOrEditAsync(update, cancellationToken);
                    }
                    catch (Exception exOne)
                    {
                        Log.Warning(exOne, "Startup sync failed for AppId {AppId}", appConfig.AppId);
                        // continue next appId
                    }
                }

                Log.Information("Startup sync completed.");
                return; // success, stop retry loop
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Startup sync attempt {Attempt}/{Max} failed", attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, attempt * 3)), cancellationToken);
            }
        }

        // IMPORTANT: do NOT throw — app continues running
        Log.Error("Startup sync failed after retries. Service will continue without synced config.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}