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
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;

    public DepartmentsController(IDepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetAll()
    {
        return Ok(await _departmentService.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DepartmentDto>> GetById(string id)
    {
        var department = await _departmentService.GetByIdAsync(id);
        if (department is null) return NotFound();
        return Ok(department);
    }

    [HttpPost]
    public async Task<ActionResult<DepartmentDto>> Create(DepartmentCreateUpdateDto dto)
    {
        var department = await _departmentService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = department.Id, version = "1.0" }, department);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, DepartmentCreateUpdateDto dto)
    {
        var result = await _departmentService.UpdateAsync(id, dto);
        return result.Succeeded ? NoContent() : NotFound();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _departmentService.DeleteAsync(id);
        return result.Succeeded ? NoContent() : NotFound();
    }
}
