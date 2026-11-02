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
public class EditModel : PageModel
{
    private readonly IGenericRepository<Section> _repository;
    private readonly IMapper _mapper;
    private readonly ActiveNameValidator _names;

    public EditModel(IGenericRepository<Section> repository, IMapper mapper, ActiveNameValidator names)
    {
        _repository = repository;
        _mapper = mapper;
        _names = names;
    }

    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty]
    public SectionCreateUpdateDto Section { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var section = await _repository.GetByIdAsync(Id);
        if (section is null)
        {
            return NotFound();
        }

        Section = new SectionCreateUpdateDto { Name = section.Name };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Only an active section has to keep its name unique.
        var current = await _repository.GetByIdAsync(Id);
        if (ModelState.IsValid && current is { IsActive: true })
        {
            await _names.ValidateAsync(ModelState, "Section.Name", NamedEntityKind.Section, Section.Name, Id.ToString());
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var section = await _repository.GetByIdAsync(Id);
        if (section is null)
        {
            return NotFound();
        }

        section.Name = Section.Name;
        _repository.Update(section);
        await _repository.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Section '{section.Name}' updated successfully.";
        return RedirectToPage("Index");
    }
}
