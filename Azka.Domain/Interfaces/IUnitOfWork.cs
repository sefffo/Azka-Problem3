using Azka.Domain.Entities;

namespace Azka.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<TEntity, TKey> GetRepository<TEntity, TKey>() where TEntity : BaseEntity<TKey>;
    Task<int> SaveChangesAsync();
}
