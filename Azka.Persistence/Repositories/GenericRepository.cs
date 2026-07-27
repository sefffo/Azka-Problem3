using System.Linq.Expressions;
using Azka.Domain.Entities;
using Azka.Domain.Interfaces;
using Azka.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Azka.Persistence.Repositories;

public class GenericRepository<TEntity, TKey>(AppDbContext context)
    : IGenericRepository<TEntity, TKey> where TEntity : BaseEntity<TKey>
{
    public async Task<IEnumerable<TEntity>> GetAllAsync()
        => await context.Set<TEntity>().ToListAsync();

    public async Task<TEntity?> GetByIdAsync(TKey id)
        => await context.Set<TEntity>().FindAsync(id);

    public async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate)
        => await context.Set<TEntity>().Where(predicate).ToListAsync();

    public async Task AddAsync(TEntity entity)
        => await context.Set<TEntity>().AddAsync(entity);

    public void Update(TEntity entity)
        => context.Set<TEntity>().Update(entity);

    public void Remove(TEntity entity)
        => context.Set<TEntity>().Remove(entity);
}
