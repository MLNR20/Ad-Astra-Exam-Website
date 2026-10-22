using AAExamManagementSystem.Models.Dtos;

namespace AAExamManagementSystem.Services;

public interface ISectionService
{
    Task<IEnumerable<SectionDto>> GetAllAsync();
    Task<SectionDto?> GetByIdAsync(int id);
    Task<SectionDto> CreateAsync(SectionCreateUpdateDto dto);
    Task<ServiceResult> UpdateAsync(int id, SectionCreateUpdateDto dto);
    Task<ServiceResult> DeleteAsync(int id);
}
