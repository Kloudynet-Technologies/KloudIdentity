using KN.KloudIdentity.Mapper.Domain.Entities;
using KN.KloudIdentity.Mapper.Infrastructure.Persistence.Abstractions;
using KN.KloudIdentity.Mapper.Infrastructure.Persistence.SQLServer;
using Microsoft.EntityFrameworkCore;

namespace KN.KloudIdentity.Mapper.Infrastructure.Persistence.Repositories;

public class AppConfigSnapshotRepository(KNContext dbContext) : RepositoryBase(dbContext), IAppConfigSnapshotRepository
{
    private readonly KNContext dbContext = dbContext;

    public void Add(AppConfigSnapshot entity)
    {
        dbContext.Add(entity);
    }

    public async Task<AppConfigSnapshot> GetAsync(string id)
    {
        var entity = await dbContext.FindAsync<AppConfigSnapshot>(id);
        if (entity == null)
        {
            throw new KeyNotFoundException($"AppConfigSnapshot with id {id} not found.");
        }

        return entity;
    }

    public async Task<IEnumerable<AppConfigSnapshot>> GetAllAsync()
    {
        return await dbContext.AppConfigSnapshots.ToListAsync();
    }

    public async Task<AppConfigSnapshot> EditAsync(AppConfigSnapshot entity)
    {
        var existingEntity = await dbContext.FindAsync<AppConfigSnapshot>(entity.Id);
        if (existingEntity == null)
        {
            throw new KeyNotFoundException($"AppConfigSnapshot with id {entity.Id} not found.");
        }

        dbContext.Entry(existingEntity).CurrentValues.SetValues(entity);
        return existingEntity;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AppConfigSnapshot?> GetByAppIdAsync(string appId)
    {
        var entity = await dbContext.AppConfigSnapshots.FirstOrDefaultAsync(e => e.AppId == appId);
        
        return entity;
    }
}