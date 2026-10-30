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
public class DetailsModel : PageModel
{
    private readonly IGenericRepository<Section> _repository;
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public DetailsModel(IGenericRepository<Section> repository, ApplicationDbContext context, IMapper mapper)
    {
        _repository = repository;
        _context = context;
        _mapper = mapper;
    }

    public SectionDto Section { get; set; } = new();

    public string DepartmentName { get; set; } = "Unassigned";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var section = await _repository.GetByIdAsync(id);
        if (section is null)
        {
            return NotFound();
        }

        Section = _mapper.Map<SectionDto>(section);
        if (section.DepartmentId is not null)
        {
            DepartmentName = await _context.Departments
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(d => d.Id == section.DepartmentId)
                .Select(d => d.Name)
                .FirstOrDefaultAsync() ?? "Unassigned";
        }

        return Page();
    }
}
