using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Entities;

namespace KN.KloudIdentity.Mapper.Infrastructure.Persistence.Abstractions;

public interface IAppConfigSnapshotRepository: IRepository<int,AppConfigSnapshot>
{
    Task<AppConfigSnapshot?> GetByAppIdAsync(string tenantId, string appId, CancellationToken cancellationToken = default);
    Task DeleteByAppIdAsync(string tenantId, string appId, CancellationToken cancellationToken = default);
    Task <AppConfig?> GetAppConfigByAppIdAsync(string appId, CancellationToken cancellationToken = default);
    Task<AppConfig?> GetAppConfigByAppIdAsync(string tenantId, string appId, CancellationToken cancellationToken = default);
}