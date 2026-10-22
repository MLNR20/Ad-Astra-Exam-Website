using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAExamManagementSystem.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public RolesController(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoleDto>>> GetAll()
    {
        var roles = await _unitOfWork.Roles.GetAllAsync();
        return Ok(_mapper.Map<IEnumerable<RoleDto>>(roles));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RoleDto>> GetById(string id)
    {
        var role = await _unitOfWork.Roles.GetByIdAsync(id);
        if (role is null) return NotFound();
        return Ok(_mapper.Map<RoleDto>(role));
    }

    [HttpPost]
    public async Task<ActionResult<RoleDto>> Create(RoleCreateUpdateDto dto)
    {
        if (await _unitOfWork.Roles.NameExistsAsync(dto.Name))
            return BadRequest($"Role '{dto.Name}' already exists.");

        var role = new ApplicationRole(dto.Name) { IsActive = dto.IsActive };
        await _unitOfWork.Roles.AddAsync(role);
        await _unitOfWork.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = role.Id, version = "1.0" }, _mapper.Map<RoleDto>(role));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, RoleCreateUpdateDto dto)
    {
        var role = await _unitOfWork.Roles.GetByIdAsync(id);
        if (role is null) return NotFound();

        if (!string.Equals(role.Name, dto.Name, StringComparison.OrdinalIgnoreCase)
            && await _unitOfWork.Roles.NameExistsAsync(dto.Name))
            return BadRequest($"Role '{dto.Name}' already exists.");

        role.Name = dto.Name;
        role.IsActive = dto.IsActive;
        role.DateUpdated = DateTime.UtcNow;
        _unitOfWork.Roles.Update(role);
        await _unitOfWork.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var role = await _unitOfWork.Roles.GetByIdAsync(id);
        if (role is null) return NotFound();

        role.IsActive = false;
        role.DateUpdated = DateTime.UtcNow;
        _unitOfWork.Roles.Update(role);
        await _unitOfWork.SaveChangesAsync();

        return NoContent();
    }
}
