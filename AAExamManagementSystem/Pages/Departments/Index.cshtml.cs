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
public class IndexModel : PageModel
{
    private readonly IGenericRepository<Department> _repository;
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public IndexModel(IGenericRepository<Department> repository, ApplicationDbContext context, IMapper mapper)
    {
        _repository = repository;
        _context = context;
        _mapper = mapper;
    }

    public IList<SectionOption> SectionOptions { get; set; } = new List<SectionOption>();

    [BindProperty]
    public List<int> SelectedSectionIds { get; set; } = new();

    public IList<DepartmentDto> Departments { get; set; } = new List<DepartmentDto>();

    [BindProperty]
    public DepartmentCreateUpdateDto NewDepartment { get; set; } = new();

    public bool ShowCreateModal { get; set; }

    public async Task OnGetAsync()
    {
        await LoadDepartmentsAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadDepartmentsAsync();
            ShowCreateModal = true;
            return Page();
        }

        var department = _mapper.Map<Department>(NewDepartment);
        await _repository.AddAsync(department);

        // Only sections not already in a department can be taken.
        var sections = await _context.Sections
            .Where(s => SelectedSectionIds.Contains(s.Id) && s.DepartmentId == null)
            .ToListAsync();
        foreach (var section in sections)
        {
            section.DepartmentId = department.Id;
        }

        await _repository.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Department '{department.Name}' created successfully.";
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostDeactivateAsync(string id)
    {
        var department = await _repository.GetByIdAsync(id);
        if (department is null)
        {
            return NotFound();
        }

        department.IsActive = false;
        department.DateUpdated = DateTime.UtcNow;
        _repository.Update(department);
        await _repository.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Department '{department.Name}' deactivated successfully.";
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostActivateAsync(string id)
    {
        var department = await _repository.GetByIdAsync(id);
        if (department is null)
        {
            return NotFound();
        }

        department.IsActive = true;
        department.DateUpdated = DateTime.UtcNow;
        _repository.Update(department);
        await _repository.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Department '{department.Name}' activated successfully.";
        return RedirectToPage("Index");
    }

    private async Task LoadDepartmentsAsync()
    {
        var departments = await _repository.GetAllIncludingInactiveAsync();
        Departments = _mapper.Map<IList<DepartmentDto>>(departments.OrderBy(d => d.Name));

        SectionOptions = await _context.Sections
            .AsNoTracking()
            .Where(s => s.DepartmentId == null)
            .OrderBy(s => s.Name)
            .Select(s => new SectionOption(s.Id, s.Name, SelectedSectionIds.Contains(s.Id)))
            .ToListAsync();
    }
}
