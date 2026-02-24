using KN.KloudIdentity.Mapper.Domain.Entities;

namespace KN.KloudIdentity.Mapper.Infrastructure.Persistence.Abstractions;

public interface IAppConfigSnapshotRepository: IRepository<AppConfigSnapshot>
{
    Task<AppConfigSnapshot?> GetByAppIdAsync(string appId);
}