using AAExamManagementSystem.Models.Entities;

namespace AAExamManagementSystem.Repository;

public interface IRoleRepository : IGenericRepository<ApplicationRole>
{
    Task<bool> NameExistsAsync(string name);
}
