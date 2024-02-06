using KN.KloudIdentity.Mapper.Domain.Application;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;

public interface IGetFullAppConfigQuery
{
    Task<AppConfig?> GetAsync(string appId, CancellationToken cancellationToken = default);
}
