using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AutoMapper;

namespace AAExamManagementSystem.Services;

public class RoleService : IRoleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public RoleService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IEnumerable<RoleDto>> GetAllAsync()
    {
        var roles = await _unitOfWork.Roles.GetAllAsync();
        return _mapper.Map<IEnumerable<RoleDto>>(roles);
    }

    public async Task<RoleDto?> GetByIdAsync(string id)
    {
        var role = await _unitOfWork.Roles.GetByIdAsync(id);
        return role is null ? null : _mapper.Map<RoleDto>(role);
    }

    public async Task<ServiceResult<RoleDto>> CreateAsync(RoleCreateUpdateDto dto)
    {
        if (await _unitOfWork.Roles.NameExistsAsync(dto.Name))
            return ServiceResult<RoleDto>.Invalid($"Role '{dto.Name}' already exists.");

        var role = new ApplicationRole(dto.Name) { IsActive = dto.IsActive };
        await _unitOfWork.Roles.AddAsync(role);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult<RoleDto>.Success(_mapper.Map<RoleDto>(role));
    }

    public async Task<ServiceResult> UpdateAsync(string id, RoleCreateUpdateDto dto)
    {
        var role = await _unitOfWork.Roles.GetByIdAsync(id);
        if (role is null) return ServiceResult.NotFound();

        if (!string.Equals(role.Name, dto.Name, StringComparison.OrdinalIgnoreCase)
            && await _unitOfWork.Roles.NameExistsAsync(dto.Name))
            return ServiceResult.Invalid($"Role '{dto.Name}' already exists.");

        role.Name = dto.Name;
        role.IsActive = dto.IsActive;
        role.DateUpdated = DateTime.UtcNow;
        _unitOfWork.Roles.Update(role);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(string id)
    {
        var role = await _unitOfWork.Roles.GetByIdAsync(id);
        if (role is null) return ServiceResult.NotFound();

        role.IsActive = false;
        role.DateUpdated = DateTime.UtcNow;
        _unitOfWork.Roles.Update(role);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult.Success();
    }
}
