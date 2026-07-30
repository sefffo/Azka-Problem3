using Azka.Domain.Interfaces;
using Azka.Domain.Specifications;
using Azka.Persistence.Data;
using Azka.Persistence.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Azka.Persistence.Repositories;

public class GenericRepository<TEntity, TKey>(
    AppDbContext context) : IGenericRepository<TEntity, TKey>
    where TEntity : class
{
    private readonly DbSet<TEntity> _dbSet = context.Set<TEntity>();

    #region Basic CRUD

    public async Task<TEntity?> GetByIdAsync(TKey id)
        => await _dbSet.FindAsync(id);

    public async Task<IReadOnlyList<TEntity>> GetAllAsync()
        => await _dbSet.ToListAsync();

    public async Task AddAsync(TEntity entity)
        => await _dbSet.AddAsync(entity);

    public void Update(TEntity entity)
        => _dbSet.Update(entity);

    public void Delete(TEntity entity)
        => _dbSet.Remove(entity);

    #endregion


    #region Specifications

    public async Task<TEntity?> GetBySpecAsync(ISpecification<TEntity> spec)
        => await SpecificationEvaluator<TEntity>
            .GetQuery(_dbSet.AsQueryable(), spec)
            .FirstOrDefaultAsync();


    //get all using the spec 
    public async Task<IReadOnlyList<TEntity>> ListAsync(ISpecification<TEntity> spec)
        => await SpecificationEvaluator<TEntity>
            .GetQuery(_dbSet.AsQueryable(), spec)
            .ToListAsync();

    public async Task<int> CountAsync(ISpecification<TEntity> spec)
        => await SpecificationEvaluator<TEntity>
            .GetQuery(_dbSet.AsQueryable(), spec)
            .CountAsync();

    public async Task<bool> AnyAsync(ISpecification<TEntity> spec)
        => await SpecificationEvaluator<TEntity>
            .GetQuery(_dbSet.AsQueryable(), spec)
            .AnyAsync();
}

#endregion