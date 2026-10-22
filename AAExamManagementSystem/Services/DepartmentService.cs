using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AutoMapper;

namespace AAExamManagementSystem.Services;

public class DepartmentService : IDepartmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public DepartmentService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IEnumerable<DepartmentDto>> GetAllAsync()
    {
        var departments = await _unitOfWork.Departments.GetAllAsync();
        return _mapper.Map<IEnumerable<DepartmentDto>>(departments);
    }

    public async Task<DepartmentDto?> GetByIdAsync(string id)
    {
        var department = await _unitOfWork.Departments.GetByIdAsync(id);
        return department is null ? null : _mapper.Map<DepartmentDto>(department);
    }

    public async Task<DepartmentDto> CreateAsync(DepartmentCreateUpdateDto dto)
    {
        var department = _mapper.Map<Department>(dto);
        await _unitOfWork.Departments.AddAsync(department);
        await _unitOfWork.SaveChangesAsync();
        return _mapper.Map<DepartmentDto>(department);
    }

    public async Task<ServiceResult> UpdateAsync(string id, DepartmentCreateUpdateDto dto)
    {
        var department = await _unitOfWork.Departments.GetByIdAsync(id);
        if (department is null) return ServiceResult.NotFound();

        department.Name = dto.Name;
        department.IsActive = dto.IsActive;
        department.DateUpdated = DateTime.UtcNow;
        _unitOfWork.Departments.Update(department);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(string id)
    {
        var department = await _unitOfWork.Departments.GetByIdAsync(id);
        if (department is null) return ServiceResult.NotFound();

        department.IsActive = false;
        department.DateUpdated = DateTime.UtcNow;
        _unitOfWork.Departments.Update(department);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult.Success();
    }
}
