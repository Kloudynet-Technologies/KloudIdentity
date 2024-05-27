using KN.KloudIdentity.Mapper.Domain.Application;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;

public interface IListApplicationsQuery
{
    Task<IList<Application>> ListAsync(CancellationToken cancellationToken = default);
}
