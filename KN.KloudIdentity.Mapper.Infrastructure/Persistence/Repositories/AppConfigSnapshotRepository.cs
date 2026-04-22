using KN.KloudIdentity.Mapper.Domain.Application;
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

    public async Task<AppConfigSnapshot> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.FindAsync<AppConfigSnapshot>(id, cancellationToken);
        return entity ?? throw new KeyNotFoundException($"AppConfigSnapshot with id {id} not found.");
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

    public async Task<AppConfigSnapshot?> GetByAppIdAsync(string tenantId, string appId,
        CancellationToken cancellationToken = default)
    {
        var entity =
            await dbContext.AppConfigSnapshots.FirstOrDefaultAsync(e => e.AppId == appId && e.TenantId == tenantId,
                cancellationToken);

        return entity;
    }

    public async Task DeleteByAppIdAsync(string tenantId, string appId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AppConfigSnapshots
            .FirstOrDefaultAsync(e => e.AppId == appId && e.TenantId == tenantId, cancellationToken);

        if (entity != null)
        {
            dbContext.AppConfigSnapshots.Remove(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<AppConfig?> GetAppConfigByAppIdAsync(string appId, CancellationToken cancellationToken = default)
    {
        var snapshot = await dbContext.AppConfigSnapshots.FirstOrDefaultAsync(e => e.AppId == appId, cancellationToken);
        if (snapshot == null)
        {
            return null;
        }

        try
        {
            var appConfig = System.Text.Json.JsonSerializer.Deserialize<AppConfig>(snapshot.ConfigJson);
            return appConfig;
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new InvalidOperationException($"Failed to deserialize AppConfig for AppId {appId}.", ex);
        }
    }

    public async Task<AppConfig?> GetAppConfigByAppIdAsync(string tenantId, string appId, CancellationToken cancellationToken = default)
    {
        var snapshot = await dbContext.AppConfigSnapshots.FirstOrDefaultAsync(e => e.AppId == appId && e.TenantId == tenantId, cancellationToken);
        if (snapshot == null)
        {
            return null;
        }

        try
        {
            var appConfig = System.Text.Json.JsonSerializer.Deserialize<AppConfig>(snapshot.ConfigJson);
            return appConfig;
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new InvalidOperationException($"Failed to deserialize AppConfig for AppId {appId}.", ex);
        }
    }
}