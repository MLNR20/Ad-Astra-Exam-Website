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
[Authorize(Roles = AppRoles.Admin)]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoleDto>>> GetAll()
    {
        return Ok(await _roleService.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RoleDto>> GetById(string id)
    {
        var role = await _roleService.GetByIdAsync(id);
        if (role is null) return NotFound();
        return Ok(role);
    }

    [HttpPost]
    public async Task<ActionResult<RoleDto>> Create(RoleCreateUpdateDto dto)
    {
        var result = await _roleService.CreateAsync(dto);
        if (!result.Succeeded) return BadRequest(result.Error);

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id, version = "1.0" }, result.Value);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, RoleCreateUpdateDto dto)
    {
        return ToActionResult(await _roleService.UpdateAsync(id, dto));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        return ToActionResult(await _roleService.DeleteAsync(id));
    }

    private IActionResult ToActionResult(ServiceResult result) => result.Status switch
    {
        ServiceStatus.NotFound => NotFound(),
        ServiceStatus.Invalid => BadRequest(result.Error),
        _ => NoContent()
    };
}
