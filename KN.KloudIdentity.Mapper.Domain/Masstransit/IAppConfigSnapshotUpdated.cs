namespace KN.KI.RabbitMQ.MessageContracts;

public interface IAppConfigSnapshotUpdated: IInterserviceRequestMsg
{
    string TenantId { get; }
    string AppId { get; }
    DateTime GeneratedAtUtc { get; }
    string PerformedBy { get; }
}