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
    public async Task AddOrEditAsync(IAppConfigSnapshotUpdated appConfigSnapshot, CancellationToken cancellationToken = default)
    { 
        ValidateMessage(appConfigSnapshot);
        var generatedAt = appConfigSnapshot.GeneratedAtUtc;
        var appConfig = JsonSerializer.Deserialize<Domain.Application.AppConfig>(appConfigSnapshot.Message);
        var json = JsonSerializer.Serialize(appConfig);
        var etag = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        
        var existingSnapshot = await configSnapshotRepository.GetByAppIdAsync(appConfigSnapshot.TenantId, appConfigSnapshot.AppId, cancellationToken);

        // INSERT
        if (existingSnapshot is null)
        {
            var newSnapshot = new AppConfigSnapshot(
                id: 0,
                tenantId: appConfigSnapshot.TenantId,
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
            await configSnapshotRepository.SaveAsync(cancellationToken);
            Log.Information(
                "AddOrEditAppConfig: Inserted new AppConfigSnapshot for CorrelationId: {CorrelationId} AppId: {AppId}, ETag: {ETag}",
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

        await configSnapshotRepository.SaveAsync(cancellationToken);

        Log.Information("AddOrEditAppConfig: Updated AppConfigSnapshot for CorrelationId: {CorrelationId} AppId: {AppId}, ETag: {ETag}",
            appConfigSnapshot.CorrelationId, appConfigSnapshot.AppId,
            etag);
        _ = CreateLogAsync(appConfigSnapshot);
    }
    
    private void ValidateMessage(IAppConfigSnapshotUpdated message)
    {
        if (string.IsNullOrWhiteSpace(message.AppId))
        {
            Log.Error("AddOrEditAppConfig: For CorrelationId: {CorrelationId} AppId: {appId} cannot be null or empty.", message.CorrelationId, message.AppId);
            throw new ArgumentException("AppId cannot be null or empty.");
        }
        if(string.IsNullOrWhiteSpace(message.TenantId))
        {
            Log.Error("AddOrEditAppConfig: For CorrelationId: {CorrelationId} AppId: {appId} TenantId cannot be null or empty.", message.CorrelationId, message.AppId);
            throw new ArgumentException("TenantId cannot be null or empty.");
        }
        if (string.IsNullOrWhiteSpace(message.Message))
        {
            Log.Error("AddOrEditAppConfig: For CorrelationId: {CorrelationId} AppId: {appId} Message cannot be null or empty.", message.CorrelationId, message.AppId);
            throw new ArgumentException("Message cannot be null or empty.");
        }
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