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
        SelectedSectionIds = await _context.Sections
            .Where(s => s.DepartmentId == Id)
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

        // A section sits in at most one department: take the ticked ones that are free or already here, and release
        // the ones that were unticked.
        var sections = await _context.Sections
            .Where(s => s.DepartmentId == Id || SelectedSectionIds.Contains(s.Id))
            .ToListAsync();
        foreach (var section in sections)
        {
            if (SelectedSectionIds.Contains(section.Id) && (section.DepartmentId is null || section.DepartmentId == Id))
            {
                section.DepartmentId = Id;
            }
            else if (!SelectedSectionIds.Contains(section.Id) && section.DepartmentId == Id)
            {
                section.DepartmentId = null;
            }
        }

        await _repository.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Department '{department.Name}' updated successfully.";
        return RedirectToPage("Index");
    }

    private async Task LoadSectionOptionsAsync()
    {
        // A section sits in one department, so those held elsewhere are left out.
        SectionOptions = await _context.Sections
            .AsNoTracking()
            .Where(s => s.DepartmentId == null || s.DepartmentId == Id)
            .OrderBy(s => s.Name)
            .Select(s => new SectionOption(s.Id, s.Name, SelectedSectionIds.Contains(s.Id)))
            .ToListAsync();
    }
}
