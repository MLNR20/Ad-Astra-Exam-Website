using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using AAExamManagementSystem.Models.Validation;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AAExamManagementSystem.Pages.Sections;

[Authorize(Roles = AppRoles.Admin)]
public class IndexModel : PageModel
{
    private readonly IGenericRepository<Section> _repository;
    private readonly IMapper _mapper;
    private readonly ActiveNameValidator _names;

    public IndexModel(IGenericRepository<Section> repository, IMapper mapper, ActiveNameValidator names)
    {
        _repository = repository;
        _mapper = mapper;
        _names = names;
    }

    public IList<SectionDto> Sections { get; set; } = new List<SectionDto>();

    [BindProperty]
    public SectionCreateUpdateDto NewSection { get; set; } = new();

    public bool ShowCreateModal { get; set; }

    public async Task OnGetAsync()
    {
        await LoadSectionsAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (ModelState.IsValid)
        {
            await _names.ValidateAsync(ModelState, "NewSection.Name", NamedEntityKind.Section, NewSection.Name);
        }

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

        if (!await _names.IsAvailableAsync(NamedEntityKind.Section, section.Name, section.Id.ToString()))
        {
            TempData["ErrorMessage"] = $"Can't activate '{section.Name}': another active section already uses that name.";
            return RedirectToPage("Index");
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
    }
}
