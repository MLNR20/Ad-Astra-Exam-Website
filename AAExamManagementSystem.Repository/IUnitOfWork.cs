using AAExamManagementSystem.Models.Entities;

namespace AAExamManagementSystem.Repository;

public interface IUnitOfWork
{
    IRoleRepository Roles { get; }
    IGenericRepository<Section> Sections { get; }
    IGenericRepository<Department> Departments { get; }
    IGenericRepository<Course> Courses { get; }
    IGenericRepository<Question> Questions { get; }
    IGenericRepository<QuestionType> QuestionTypes { get; }
    IGenericRepository<Choice> Choices { get; }

    Task<int> SaveChangesAsync();
}
