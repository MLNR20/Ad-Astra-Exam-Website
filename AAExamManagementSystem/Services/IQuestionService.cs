using AAExamManagementSystem.Models.Dtos;

namespace AAExamManagementSystem.Services;

public interface IQuestionService
{
    Task<IEnumerable<QuestionDto>> GetAllAsync();
    Task<QuestionDto?> GetByIdAsync(Guid id);
    Task<ServiceResult<QuestionDto>> CreateAsync(QuestionCreateUpdateDto dto);
    Task<ServiceResult> UpdateAsync(Guid id, QuestionCreateUpdateDto dto);
    Task<ServiceResult> DeleteAsync(Guid id);
}
