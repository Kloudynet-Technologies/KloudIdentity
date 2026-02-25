using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KN.KI.LogAggregator.Library;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.Domain.Entities;
using KN.KloudIdentity.Mapper.Infrastructure.Persistence.Abstractions;
using Serilog;

namespace KN.KloudIdentity.Mapper.Common.AppConfig;

public class AddOrEditAppConfig(
    IAppConfigSnapshotRepository configSnapshotRepository,
    IKloudIdentityLogger logger
) : IAddOrEditAppConfig
{
    public async Task AddOrEditAsync(IAppConfigSnapshotUpdated appConfigSnapshot)
    { 
        var generatedAt = appConfigSnapshot.GeneratedAtUtc;
        var appConfig = JsonSerializer.Deserialize<Domain.Application.AppConfig>(appConfigSnapshot.Message);
        var json = JsonSerializer.Serialize(appConfig);
        var etag = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        
        var existingSnapshot = await configSnapshotRepository.GetByAppIdAsync(appConfigSnapshot.AppId);

        // INSERT
        if (existingSnapshot is null)
        {
            var newSnapshot = new AppConfigSnapshot(
                id: 0,
                appId: appConfigSnapshot.AppId,
                etag: etag,
                configJson: json,
                generatedDate: generatedAt,
                createdDate: DateTime.UtcNow,
                createdBy: appConfigSnapshot.PerformedBy ?? "system",
                modifiedDate: null,
                modifiedBy: null
            );

            configSnapshotRepository.Add(newSnapshot);
            await configSnapshotRepository.SaveAsync();
            Log.Information(
                "Inserted new AppConfigSnapshot for CorrelationId: {CorrelationId} AppId: {AppId}, ETag: {ETag}",
                appConfigSnapshot.CorrelationId, appConfigSnapshot.AppId,
                etag);
            _ = CreateLogAsync(appConfigSnapshot);
            return;
        }

        // Idempotency checks
        if (etag == existingSnapshot.Etag)
            return;

        if (generatedAt <= existingSnapshot.GeneratedDate)
            return;

        // UPDATE (preferred: update tracked entity)
        existingSnapshot.UpdateSnapshot(
            etag: etag,
            configJson: json,
            generatedDate: generatedAt,
            modifiedBy: appConfigSnapshot.PerformedBy ?? "system"
        );

        await configSnapshotRepository.EditAsync(existingSnapshot);

        await configSnapshotRepository.SaveAsync();

        Log.Information("Updated AppConfigSnapshot for CorrelationId: {CorrelationId} AppId: {AppId}, ETag: {ETag}",
            appConfigSnapshot.CorrelationId, appConfigSnapshot.AppId,
            etag);
        _ = CreateLogAsync(appConfigSnapshot);
    }

    private async Task CreateLogAsync(IAppConfigSnapshotUpdated appConfigSnapshot)
    {
        await logger.CreateLogAsync(new CreateLogEntity(
            AppId: appConfigSnapshot.AppId,
            Type: nameof(LogType.Edit),
            Severity: LogSeverities.Information,
            EventInfo:
            $"Processed AppConfigSnapshotUpdated for CorrelationId: {appConfigSnapshot.CorrelationId} AppId: {appConfigSnapshot.AppId}",
            Message:
            $"Processed AppConfigSnapshotUpdated for CorrelationId: {appConfigSnapshot.CorrelationId} AppId: {appConfigSnapshot.AppId}",
            CorrelationId: appConfigSnapshot.CorrelationId!,
            LoggerName: "KN.KloudIdentity.Mapper",
            CreatedAt: DateTime.UtcNow,
            CreatedBy: appConfigSnapshot.PerformedBy ?? "system",
            Exception: null,
            ExceptionInfo: null
        ));
    }
}