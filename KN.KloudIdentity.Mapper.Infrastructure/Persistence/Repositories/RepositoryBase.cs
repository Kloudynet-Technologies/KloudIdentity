using KN.KloudIdentity.Mapper.Infrastructure.Persistence.SQLServer;

namespace KN.KloudIdentity.Mapper.Infrastructure.Persistence.Repositories;

public class RepositoryBase(KNContext dbContext)
{
    public async Task SaveGlobalChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}