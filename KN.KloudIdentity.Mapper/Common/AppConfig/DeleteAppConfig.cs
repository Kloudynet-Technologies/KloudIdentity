using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.Infrastructure.Persistence.Abstractions;
using Serilog;

namespace KN.KloudIdentity.Mapper.Common.AppConfig;

public class DeleteAppConfig(
    IAppConfigSnapshotRepository appConfigSnapshotRepository,
    IKloudIdentityLogger logger
) : IDeleteAppConfig
{
    public async Task DeleteAsync(IAppConfigSnapshotUpdated snapshotUpdated,
        CancellationToken cancellationToken = default)
    {
        ValidateMessage(snapshotUpdated);
        await appConfigSnapshotRepository.DeleteByAppIdAsync(snapshotUpdated.TenantId, snapshotUpdated.AppId,
            cancellationToken);
        Log.Information("Deleted AppConfigSnapshot for appId {AppId}", snapshotUpdated.AppId);

        _ = CreateLogAsync(snapshotUpdated);
    }

    private static void ValidateMessage(IAppConfigSnapshotUpdated message)
    {
        if (string.IsNullOrWhiteSpace(message.AppId))
        {
            Log.Error("DeleteAppConfig: For CorrelationId: {CorrelationId} AppId: {appId} cannot be null or empty.",
                message.CorrelationId, message.AppId);
            throw new ArgumentException("DeleteAppConfig: AppId cannot be null or empty.");
        }

        if (string.IsNullOrWhiteSpace(message.TenantId))
        {
            Log.Error("For CorrelationId: {CorrelationId} AppId: {appId} TenantId cannot be null or empty.",
                message.CorrelationId, message.AppId);
            throw new ArgumentException("DeleteAppConfig: TenantId cannot be null or empty.");
        }
    }

    private async Task CreateLogAsync(IAppConfigSnapshotUpdated snapshotUpdated)
    {
        await logger.CreateLogAsync(new CreateLogEntity(
            AppId: snapshotUpdated.AppId,
            Type: nameof(LogType.Delete),
            Severity: LogSeverities.Information,
            EventInfo:
            $"Deleted AppConfigSnapshotUpdated for CorrelationId: {snapshotUpdated.CorrelationId} AppId: {snapshotUpdated.AppId}",
            Message: $"Deleted AppConfigSnapshot for appId {snapshotUpdated.AppId}",
            CorrelationId: snapshotUpdated.CorrelationId!,
            LoggerName: "KN.KloudIdentity.Mapper",
            CreatedAt: DateTime.UtcNow,
            CreatedBy: snapshotUpdated.PerformedBy ?? "system",
            Exception: null,
            ExceptionInfo: null
        ));
    }
}