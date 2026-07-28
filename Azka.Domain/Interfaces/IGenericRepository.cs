using Azka.Domain.Specifications;
using System.Linq.Expressions;

namespace Azka.Domain.Interfaces;

public interface IGenericRepository<TEntity, TKey>
    where TEntity : class
{
    // ── Basic CRUD ────────────────────────────────────────────────────────────
    Task<TEntity?> GetByIdAsync(TKey id);
    Task<IReadOnlyList<TEntity>> GetAllAsync();
    Task AddAsync(TEntity entity);
    void Update(TEntity entity);
    void Delete(TEntity entity);

    // ── Specification-based queries ───────────────────────────────────────────
    Task<TEntity?> GetBySpecAsync(ISpecification<TEntity> spec);
    Task<IReadOnlyList<TEntity>> ListAsync(ISpecification<TEntity> spec);
    Task<int> CountAsync(ISpecification<TEntity> spec);
    Task<bool> AnyAsync(ISpecification<TEntity> spec);
}
