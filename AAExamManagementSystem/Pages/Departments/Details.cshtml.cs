using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Pages.Departments;

[Authorize(Roles = AppRoles.Admin)]
public class DetailsModel : PageModel
{
    private readonly IGenericRepository<Department> _repository;
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public DetailsModel(IGenericRepository<Department> repository, ApplicationDbContext context, IMapper mapper)
    {
        _repository = repository;
        _context = context;
        _mapper = mapper;
    }

    public DepartmentDto Department { get; set; } = new();

    public string SectionNames { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var department = await _repository.GetByIdAsync(id);
        if (department is null)
        {
            return NotFound();
        }

        Department = _mapper.Map<DepartmentDto>(department);
        SectionNames = string.Join(", ", await _context.Sections
            .AsNoTracking()
            .Where(s => s.DepartmentId == id)
            .OrderBy(s => s.Name)
            .Select(s => s.Name)
            .ToListAsync());
        return Page();
    }
}
