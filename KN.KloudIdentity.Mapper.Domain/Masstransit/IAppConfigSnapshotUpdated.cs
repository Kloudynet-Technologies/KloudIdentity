namespace KN.KI.RabbitMQ.MessageContracts;

public interface IAppConfigSnapshotUpdated: IInterserviceRequestMsg
{
    string AppId { get; }
    DateTime GeneratedAtUtc { get; }
    string ETag { get; }
    string PerformedBy { get; }
}