using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Pages.Sections;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly IGenericRepository<Section> _repository;
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public IndexModel(IGenericRepository<Section> repository, ApplicationDbContext context, IMapper mapper)
    {
        _repository = repository;
        _context = context;
        _mapper = mapper;
    }

    public IList<SectionDto> Sections { get; set; } = new List<SectionDto>();

    /// <summary>The department each assigned section belongs to, by section id.</summary>
    public Dictionary<int, string> DepartmentNames { get; set; } = new();

    [BindProperty]
    public SectionCreateUpdateDto NewSection { get; set; } = new();

    public bool ShowCreateModal { get; set; }

    public async Task OnGetAsync()
    {
        await LoadSectionsAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadSectionsAsync();
            ShowCreateModal = true;
            return Page();
        }

        var section = _mapper.Map<Section>(NewSection);
        await _repository.AddAsync(section);
        await _repository.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Section '{section.Name}' created successfully.";
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int id)
    {
        var section = await _repository.GetByIdAsync(id);
        if (section is null)
        {
            return NotFound();
        }

        section.IsActive = false;
        _repository.Update(section);
        await _repository.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Section '{section.Name}' deactivated successfully.";
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostActivateAsync(int id)
    {
        var section = await _repository.GetByIdAsync(id);
        if (section is null)
        {
            return NotFound();
        }

        section.IsActive = true;
        _repository.Update(section);
        await _repository.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Section '{section.Name}' activated successfully.";
        return RedirectToPage("Index");
    }

    private async Task LoadSectionsAsync()
    {
        var sections = await _repository.GetAllIncludingInactiveAsync();
        Sections = _mapper.Map<IList<SectionDto>>(sections.OrderBy(s => s.Name));

        // Inactive departments still count: the section is assigned to them until someone moves it.
        DepartmentNames = await _context.Sections
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.DepartmentId != null)
            .Select(s => new { s.Id, DepartmentName = s.Department!.Name })
            .ToDictionaryAsync(x => x.Id, x => x.DepartmentName);
    }
}
