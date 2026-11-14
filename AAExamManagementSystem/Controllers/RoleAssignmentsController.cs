using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AAExamManagementSystem.Models.Entities;

namespace AAExamManagementSystem.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Roles = AppRoles.Admin)]
public class RoleAssignmentsController : ControllerBase
{
    private readonly IRoleAssignmentService _roleAssignmentService;
    private readonly UserManager<ApplicationUser> _userManager;

    public RoleAssignmentsController(IRoleAssignmentService roleAssignmentService, UserManager<ApplicationUser> userManager)
    {
        _roleAssignmentService = roleAssignmentService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoleAssignmentDto>>> GetAll()
    {
        return Ok(await _roleAssignmentService.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RoleAssignmentDto>> GetById(string id)
    {
        var assignment = await _roleAssignmentService.GetByIdAsync(id);
        if (assignment is null) return NotFound();

        return Ok(assignment);
    }

    [HttpPost]
    public async Task<ActionResult<RoleAssignmentDto>> Create(RoleAssignmentCreateDto dto)
    {
        var result = await _roleAssignmentService.CreateAsync(dto);
        if (!result.Succeeded) return BadRequest(result.Error);

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id, version = "1.0" }, result.Value);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, RoleAssignmentEditDto dto)
    {
        return ToActionResult(await _roleAssignmentService.ChangeRoleAsync(id, dto.RoleId, _userManager.GetUserId(User)));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        return ToActionResult(await _roleAssignmentService.DeactivateAsync(id, _userManager.GetUserId(User)));
    }

    private IActionResult ToActionResult(ServiceResult result) => result.Status switch
    {
        ServiceStatus.NotFound => NotFound(),
        ServiceStatus.Invalid => BadRequest(result.Error),
        ServiceStatus.Forbidden => Forbid(),
        _ => NoContent()
    };
}
