using KN.KloudIdentity.Mapper.Domain.Setting;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;

public interface IGetApplicationSettingQuery
{
    Task<ApplicationSetting?> GetAsync(string appId, CancellationToken cancellationToken = default);
}
