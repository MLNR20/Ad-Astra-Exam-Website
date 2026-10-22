using AAExamManagementSystem.Models.Dtos;

namespace AAExamManagementSystem.Services;

public interface IDepartmentService
{
    Task<IEnumerable<DepartmentDto>> GetAllAsync();
    Task<DepartmentDto?> GetByIdAsync(string id);
    Task<DepartmentDto> CreateAsync(DepartmentCreateUpdateDto dto);
    Task<ServiceResult> UpdateAsync(string id, DepartmentCreateUpdateDto dto);
    Task<ServiceResult> DeleteAsync(string id);
}
