using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Entities;

namespace KN.KloudIdentity.Mapper.Infrastructure.Persistence.Abstractions;

public interface IAppConfigSnapshotRepository: IRepository<AppConfigSnapshot>
{
    Task<AppConfigSnapshot?> GetByAppIdAsync(string appId);
    Task DeleteByAppIdAsync(string appId);
    Task<AppConfig?> GetAppConfigByAppIdAsync(string appId);
}