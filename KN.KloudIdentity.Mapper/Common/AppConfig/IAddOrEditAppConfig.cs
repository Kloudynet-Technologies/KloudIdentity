using KN.KI.RabbitMQ.MessageContracts;

namespace KN.KloudIdentity.Mapper.Common.AppConfig;

public interface IAddOrEditAppConfig
{
    Task AddOrEditAsync(IAppConfigSnapshotUpdated appConfigSnapshotUpdated, CancellationToken cancellationToken = default);
}