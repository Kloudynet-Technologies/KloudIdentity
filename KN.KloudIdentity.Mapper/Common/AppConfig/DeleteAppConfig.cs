using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.Infrastructure.Persistence.Abstractions;
using Microsoft.Graph.Security.Alerts_v2;
using Serilog;

namespace KN.KloudIdentity.Mapper.Common.AppConfig;

public class DeleteAppConfig(
    IAppConfigSnapshotRepository appConfigSnapshotRepository,
    IKloudIdentityLogger logger
    ) : IDeleteAppConfig
{
    public async Task DeleteAsync(IAppConfigSnapshotUpdated snapshotUpdated, CancellationToken cancellationToken = default)
    {
        await appConfigSnapshotRepository.DeleteByAppIdAsync(snapshotUpdated.AppId, cancellationToken);
        Log.Information("Deleted AppConfigSnapshot for appId {AppId}", snapshotUpdated.AppId);
        
        _= CreateLogAsync(snapshotUpdated);
    }
    
    private async Task CreateLogAsync(IAppConfigSnapshotUpdated snapshotUpdated)
    {
        await logger.CreateLogAsync(new CreateLogEntity(
            AppId: snapshotUpdated.AppId,
            Type: nameof(LogType.Delete),
            Severity: LogSeverities.Information,
            EventInfo:$"Deleted AppConfigSnapshotUpdated for CorrelationId: {snapshotUpdated.CorrelationId} AppId: {snapshotUpdated.AppId}",
            Message: "Deleted AppConfigSnapshot for appId {AppId}",
            CorrelationId: snapshotUpdated.CorrelationId!,
            LoggerName: "KN.KloudIdentity.Mapper",
            CreatedAt: DateTime.UtcNow,
            CreatedBy: snapshotUpdated.PerformedBy ?? "system",
            Exception: null,
            ExceptionInfo: null
        ));
    }
}