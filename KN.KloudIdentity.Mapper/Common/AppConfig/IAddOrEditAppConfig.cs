using KN.KI.RabbitMQ.MessageContracts;
using Microsoft.AspNetCore.Http;

namespace KN.KloudIdentity.Mapper.Common.AppConfig;

public interface IAddOrEditAppConfig
{
    Task AddOrEditAsync(IAppConfigSnapshotUpdated appConfigSnapshotUpdated);
}