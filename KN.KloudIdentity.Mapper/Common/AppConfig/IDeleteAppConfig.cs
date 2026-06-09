using KN.KI.RabbitMQ.MessageContracts;

namespace KN.KloudIdentity.Mapper.Common.AppConfig;

public interface IDeleteAppConfig
{
    Task DeleteAsync(IAppConfigSnapshotUpdated snapshotUpdated, CancellationToken cancellationToken = default);
}