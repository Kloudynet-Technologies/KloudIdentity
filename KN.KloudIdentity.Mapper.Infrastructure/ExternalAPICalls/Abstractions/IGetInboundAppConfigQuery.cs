using KN.KloudIdentity.Mapper.Domain.Inbound;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions
{
    public interface IGetInboundAppConfigQuery
    {
        Task<InboundConfig> GetAsync(string appId, CancellationToken cancellationToken = default);
    }
}
