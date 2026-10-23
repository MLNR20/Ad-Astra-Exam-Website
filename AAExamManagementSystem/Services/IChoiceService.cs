using AAExamManagementSystem.Models.Dtos;

namespace AAExamManagementSystem.Services;

public interface IChoiceService
{
    Task<IEnumerable<ChoiceDto>> GetAllAsync();
    Task<ChoiceDto?> GetByIdAsync(Guid id);
    Task<ChoiceDto> CreateAsync(ChoiceCreateUpdateDto dto);
    Task<ServiceResult> UpdateAsync(Guid id, ChoiceCreateUpdateDto dto);
    Task<ServiceResult> DeleteAsync(Guid id);
}
