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
public class DepartmentsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public DepartmentsController(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetAll()
    {
        var departments = await _unitOfWork.Departments.GetAllAsync();
        return Ok(_mapper.Map<IEnumerable<DepartmentDto>>(departments));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DepartmentDto>> GetById(string id)
    {
        var department = await _unitOfWork.Departments.GetByIdAsync(id);
        if (department is null) return NotFound();
        return Ok(_mapper.Map<DepartmentDto>(department));
    }

    [HttpPost]
    public async Task<ActionResult<DepartmentDto>> Create(DepartmentCreateUpdateDto dto)
    {
        var department = _mapper.Map<Department>(dto);
        await _unitOfWork.Departments.AddAsync(department);
        await _unitOfWork.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = department.Id, version = "1.0" }, _mapper.Map<DepartmentDto>(department));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, DepartmentCreateUpdateDto dto)
    {
        var department = await _unitOfWork.Departments.GetByIdAsync(id);
        if (department is null) return NotFound();

        department.Name = dto.Name;
        department.IsActive = dto.IsActive;
        department.DateUpdated = DateTime.UtcNow;
        _unitOfWork.Departments.Update(department);
        await _unitOfWork.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var department = await _unitOfWork.Departments.GetByIdAsync(id);
        if (department is null) return NotFound();

        department.IsActive = false;
        department.DateUpdated = DateTime.UtcNow;
        _unitOfWork.Departments.Update(department);
        await _unitOfWork.SaveChangesAsync();
        return NoContent();
    }
}
