using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Validation;
using AAExamManagementSystem.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAExamManagementSystem.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Editor}")]
public class SectionsController : ControllerBase
{
    private readonly ISectionService _sectionService;

    public SectionsController(ISectionService sectionService)
    {
        _sectionService = sectionService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SectionDto>>> GetAll()
    {
        return Ok(await _sectionService.GetAllAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SectionDto>> GetById(int id)
    {
        var section = await _sectionService.GetByIdAsync(id);
        if (section is null) return NotFound();
        return Ok(section);
    }

    [HttpPost]
    [TypeFilter(typeof(UniqueActiveNameFilter), Arguments = new object[] { NamedEntityKind.Section })]
    public async Task<ActionResult<SectionDto>> Create(SectionCreateUpdateDto dto)
    {
        var section = await _sectionService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = section.Id, version = "1.0" }, section);
    }

    [HttpPut("{id:int}")]
    [TypeFilter(typeof(UniqueActiveNameFilter), Arguments = new object[] { NamedEntityKind.Section })]
    public async Task<IActionResult> Update(int id, SectionCreateUpdateDto dto)
    {
        var result = await _sectionService.UpdateAsync(id, dto);
        return result.Succeeded ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _sectionService.DeleteAsync(id);
        return result.Succeeded ? NoContent() : NotFound();
    }
}
