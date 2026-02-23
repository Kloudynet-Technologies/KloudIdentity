namespace KN.KI.RabbitMQ.MessageContracts;

public interface IAppConfigSnapshotUpdated: IInterserviceRequestMsg
{
    DateTime GeneratedAtUtc { get; }
    string ETag { get; }
}