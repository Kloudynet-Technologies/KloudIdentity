using KN.KloudIdentity.Mapper.Domain.License;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;

public interface ILicenseStatusCheckQuery
{
    Task<LicenseStatus> IsLicenseValidAsync(CancellationToken cancellationToken = default);
}