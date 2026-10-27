namespace AAExamManagementSystem.Repository;

public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id);
    /// <summary>Every row the global query filters let through, i.e. active ones for soft-deleted entities.</summary>
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>Every row, including soft-deleted ones. For admin lists and name lookups, not for dropdowns.</summary>
    Task<IEnumerable<T>> GetAllIncludingInactiveAsync();
    Task AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
    Task<int> SaveChangesAsync();
}
