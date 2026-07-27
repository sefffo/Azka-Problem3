using Azka.Domain.Entities;
using Azka.Domain.Interfaces;
using Azka.Persistence.Data;

namespace Azka.Persistence.Repositories;

public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = new();

    public IGenericRepository<TEntity, TKey> GetRepository<TEntity, TKey>()
        where TEntity : BaseEntity<TKey>
    {
        var type = typeof(TEntity);
        if (_repositories.TryGetValue(type, out var existing))
            return (IGenericRepository<TEntity, TKey>)existing;

        var repo = new GenericRepository<TEntity, TKey>(context);
        _repositories[type] = repo;
        return repo;
    }

    public async Task<int> SaveChangesAsync()
        => await context.SaveChangesAsync();

    public void Dispose()
        => context.Dispose();
}
