using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Repository;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    private readonly DbContext _context;
    private readonly DbSet<T> _dbSet;

    public GenericRepository(DbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    // FindAsync applies global query filters, which would make a deactivated row impossible to open or reactivate,
    // so fall back to a key lookup that ignores them.
    public async Task<T?> GetByIdAsync(object id)
    {
        var found = await _dbSet.FindAsync(id);
        if (found is not null)
        {
            return found;
        }

        var key = _context.Model.FindEntityType(typeof(T))?.FindPrimaryKey()?.Properties;
        if (key is not { Count: 1 } || id.GetType() != key[0].ClrType)
        {
            return null;
        }

        var entity = Expression.Parameter(typeof(T), "e");
        var keyValue = Expression.Call(typeof(EF), nameof(EF.Property), new[] { key[0].ClrType }, entity, Expression.Constant(key[0].Name));
        var predicate = Expression.Lambda<Func<T, bool>>(Expression.Equal(keyValue, Expression.Constant(id, key[0].ClrType)), entity);

        return await _dbSet.IgnoreQueryFilters().FirstOrDefaultAsync(predicate);
    }

    public async Task<IEnumerable<T>> GetAllAsync() => await _dbSet.ToListAsync();

    public async Task<IEnumerable<T>> GetAllIncludingInactiveAsync() => await _dbSet.IgnoreQueryFilters().ToListAsync();

    public virtual async Task AddAsync(T entity) => await _dbSet.AddAsync(entity);

    public virtual void Update(T entity) => _dbSet.Update(entity);

    public void Remove(T entity) => _dbSet.Remove(entity);

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();
}
