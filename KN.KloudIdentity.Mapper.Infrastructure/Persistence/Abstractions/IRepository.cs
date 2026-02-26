namespace KN.KloudIdentity.Mapper.Infrastructure.Persistence.Abstractions;

public interface IRepository<T> where T : class
{
    void Add(T entity);

    Task<T> GetAsync(string id, CancellationToken none);

    Task<IEnumerable<T>> GetAllAsync();

    Task<T> EditAsync(T entity);

    Task SaveAsync(CancellationToken cancellationToken = default);
}