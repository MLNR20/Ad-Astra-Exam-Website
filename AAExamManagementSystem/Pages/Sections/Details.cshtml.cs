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

    public string DepartmentNames { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var section = await _repository.GetByIdAsync(id);
        if (section is null)
        {
            return NotFound();
        }

        Section = _mapper.Map<SectionDto>(section);
        DepartmentNames = string.Join(", ", await _context.Sections
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.Id == id)
            .SelectMany(s => s.Departments)
            .OrderBy(d => d.Name)
            .Select(d => d.Name)
            .ToListAsync());

        return Page();
    }
}
