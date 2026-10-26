using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAExamManagementSystem.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Editor}")]
public class ChoicesController : ControllerBase
{
    private readonly IChoiceService _choiceService;

    public ChoicesController(IChoiceService choiceService)
    {
        _choiceService = choiceService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ChoiceDto>>> GetAll()
    {
        return Ok(await _choiceService.GetAllAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ChoiceDto>> GetById(Guid id)
    {
        var choice = await _choiceService.GetByIdAsync(id);
        if (choice is null) return NotFound();
        return Ok(choice);
    }

    [HttpPost]
    public async Task<ActionResult<ChoiceDto>> Create(ChoiceCreateUpdateDto dto)
    {
        var choice = await _choiceService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = choice.Id, version = "1.0" }, choice);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, ChoiceCreateUpdateDto dto)
    {
        var result = await _choiceService.UpdateAsync(id, dto);
        return result.Succeeded ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _choiceService.DeleteAsync(id);
        return result.Succeeded ? NoContent() : NotFound();
    }
}
