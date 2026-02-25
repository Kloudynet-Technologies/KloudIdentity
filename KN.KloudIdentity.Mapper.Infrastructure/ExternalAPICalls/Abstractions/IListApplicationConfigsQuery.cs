using KN.KloudIdentity.Mapper.Domain.Application;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;

public interface IListApplicationConfigsQuery
{
    Task<List<AppConfig>> ListAsync(CancellationToken cancellationToken = default);
}