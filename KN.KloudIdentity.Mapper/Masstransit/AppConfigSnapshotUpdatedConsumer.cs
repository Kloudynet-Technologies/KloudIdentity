using KN.KI.RabbitMQ.MessageContracts;
using KN.KloudIdentity.Mapper.Common.AppConfig;
using MassTransit;
using Serilog;

namespace KN.KloudIdentity.Mapper.Masstransit;

public class AppConfigSnapshotUpdatedConsumer(
    IAddOrEditAppConfig addOrEditAppConfig,
    IDeleteAppConfig deleteAppConfig
    )
    : IConsumer<IAppConfigSnapshotUpdated>
{
    public async Task Consume(ConsumeContext<IAppConfigSnapshotUpdated> context)
    {
        var appConfigSnapshot = context.Message;
        switch (appConfigSnapshot.Action)
        {
            case "Add":
            case "Edit":
                ValidateMessage(appConfigSnapshot);
                await addOrEditAppConfig.AddOrEditAsync(appConfigSnapshot);
                break;
            case "Delete":
                if(string.IsNullOrWhiteSpace(appConfigSnapshot.AppId))
                {
                    Log.Error("For CorrelationId: {CorrelationId} AppId: {appId} cannot be null or empty for delete action.", appConfigSnapshot.CorrelationId, appConfigSnapshot.AppId);
                    throw new ArgumentException("AppId cannot be null or empty for delete action.");
                }
                await deleteAppConfig.DeleteAsync(appConfigSnapshot);
                break;
            default:
                Log.Error("For CorrelationId: {CorrelationId} AppId: {AppId} received invalid action: {Action}", appConfigSnapshot.CorrelationId, appConfigSnapshot.AppId, appConfigSnapshot.Action);
                throw new ArgumentException($"Invalid action: {appConfigSnapshot.Action}");
        }
    }

    private static void ValidateMessage(IAppConfigSnapshotUpdated message)
    {
        if (string.IsNullOrWhiteSpace(message.AppId))
        {
            Log.Error("For CorrelationId: {CorrelationId} AppId: {appId} cannot be null or empty.", message.CorrelationId, message.AppId);
            throw new ArgumentException("AppId cannot be null or empty.");
        }

        if (string.IsNullOrWhiteSpace(message.Message))
        {
            Log.Error("For CorrelationId: {CorrelationId} AppId: {appId} Message cannot be null or empty.", message.CorrelationId, message.AppId);
            throw new ArgumentException("Message cannot be null or empty.");
        }

        if (message.GeneratedAtUtc == default)
        {
            Log.Error("For CorrelationId: {CorrelationId} AppId: {appId} GeneratedAtUtc is not set.", message.CorrelationId, message.AppId);
            throw new ArgumentException("GeneratedAtUtc must be a valid date.");
        }

        // ✅ JSON validity check
        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(message.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            Log.Error(ex, "Error parsing correlationId: {correlationId} appId: {appId} message: {Message}", message.CorrelationId, message.AppId, message.Message);
            throw new ArgumentException("Message must be valid JSON.", ex);
        }
    }
}