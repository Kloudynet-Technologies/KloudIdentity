using KN.KloudIdentity.Mapper.Domain.As400;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;

public interface IListAs400GroupsQuery
{
    Task<IList<As400Group>> ListAsync(string appId, CancellationToken cancellationToken = default);
}