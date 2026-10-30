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
public class EditModel : PageModel
{
    private readonly IGenericRepository<Department> _repository;
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public EditModel(IGenericRepository<Department> repository, ApplicationDbContext context, IMapper mapper)
    {
        _repository = repository;
        _context = context;
        _mapper = mapper;
    }

    public IList<SectionOption> SectionOptions { get; set; } = new List<SectionOption>();

    [BindProperty]
    public List<int> SelectedSectionIds { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;

    [BindProperty]
    public DepartmentCreateUpdateDto Department { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var department = await _repository.GetByIdAsync(Id);
        if (department is null)
        {
            return NotFound();
        }

        Department = new DepartmentCreateUpdateDto { Name = department.Name, IsActive = department.IsActive };
        SelectedSectionIds = await _context.Departments
            .IgnoreQueryFilters()
            .Where(d => d.Id == Id)
            .SelectMany(d => d.Sections)
            .Where(s => s.IsActive)
            .Select(s => s.Id)
            .ToListAsync();
        await LoadSectionOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadSectionOptionsAsync();
            return Page();
        }

        var department = await _repository.GetByIdAsync(Id);
        if (department is null)
        {
            return NotFound();
        }

        department.Name = Department.Name;
        department.IsActive = Department.IsActive;
        department.DateUpdated = DateTime.UtcNow;
        _repository.Update(department);

        // A section can sit in many departments, so this only changes this department's own list.
        await _context.Entry(department).Collection(d => d.Sections).LoadAsync();
        var chosen = SelectedSectionIds.Distinct().ToList();
        foreach (var section in department.Sections.Where(s => !chosen.Contains(s.Id)).ToList())
        {
            department.Sections.Remove(section);
        }

        var currentIds = department.Sections.Select(s => s.Id).ToList();
        var toAdd = await _context.Sections
            .Where(s => chosen.Contains(s.Id) && !currentIds.Contains(s.Id))
            .ToListAsync();
        foreach (var section in toAdd)
        {
            department.Sections.Add(section);
        }

        await _repository.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Department '{department.Name}' updated successfully.";
        return RedirectToPage("Index");
    }

    private async Task LoadSectionOptionsAsync()
    {
        SectionOptions = await _context.Sections
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SectionOption(s.Id, s.Name, SelectedSectionIds.Contains(s.Id)))
            .ToListAsync();
    }
}
