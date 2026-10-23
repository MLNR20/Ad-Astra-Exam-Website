using AAExamManagementSystem.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Repository;

public class UnitOfWork : IUnitOfWork
{
    private readonly DbContext _context;
    private readonly ILookupNormalizer _normalizer;

    private IRoleRepository? _roles;
    private IGenericRepository<Section>? _sections;
    private IGenericRepository<Department>? _departments;
    private IGenericRepository<Course>? _courses;
    private IGenericRepository<Question>? _questions;
    private IGenericRepository<QuestionType>? _questionTypes;
    private IGenericRepository<Choice>? _choices;

    public UnitOfWork(DbContext context, ILookupNormalizer normalizer)
    {
        _context = context;
        _normalizer = normalizer;
    }

    public IRoleRepository Roles => _roles ??= new RoleRepository(_context, _normalizer);

    public IGenericRepository<Section> Sections => _sections ??= new GenericRepository<Section>(_context);

    public IGenericRepository<Department> Departments => _departments ??= new GenericRepository<Department>(_context);

    public IGenericRepository<Course> Courses => _courses ??= new GenericRepository<Course>(_context);

    public IGenericRepository<Question> Questions => _questions ??= new GenericRepository<Question>(_context);

    public IGenericRepository<QuestionType> QuestionTypes => _questionTypes ??= new GenericRepository<QuestionType>(_context);

    public IGenericRepository<Choice> Choices => _choices ??= new GenericRepository<Choice>(_context);

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();
}
