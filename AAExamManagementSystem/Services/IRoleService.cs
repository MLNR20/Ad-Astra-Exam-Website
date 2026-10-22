using AAExamManagementSystem.Models.Dtos;

namespace AAExamManagementSystem.Services;

public interface IRoleService
{
    Task<IEnumerable<RoleDto>> GetAllAsync();
    Task<RoleDto?> GetByIdAsync(string id);
    Task<ServiceResult<RoleDto>> CreateAsync(RoleCreateUpdateDto dto);
    Task<ServiceResult> UpdateAsync(string id, RoleCreateUpdateDto dto);
    Task<ServiceResult> DeleteAsync(string id);
}
