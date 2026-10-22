using AAExamManagementSystem.Models.Entities;

namespace AAExamManagementSystem.Repository;

public interface IUnitOfWork
{
    IRoleRepository Roles { get; }
    IGenericRepository<Section> Sections { get; }
    IGenericRepository<Department> Departments { get; }

    Task<int> SaveChangesAsync();
}
