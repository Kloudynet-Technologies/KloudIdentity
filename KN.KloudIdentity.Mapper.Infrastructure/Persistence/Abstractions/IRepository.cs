namespace KN.KloudIdentity.Mapper.Infrastructure.Persistence.Abstractions;

public interface IRepository<TKey, T> where T : class
{
    void Add(T entity);

    Task<T> GetAsync(TKey id, CancellationToken cancellationToken = default);

    Task<IEnumerable<T>> GetAllAsync();

    Task<T> EditAsync(T entity);

    Task SaveAsync(CancellationToken cancellationToken = default);
}