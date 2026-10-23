using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AAExamManagementSystem.Services;

public interface IQuestionChoiceService
{
    Task<IList<int>> GetChoiceTypeIdsAsync();
    Task<List<ChoiceDto>> GetChoicesAsync(Guid questionId);
    Task NormalizeAndValidateAsync(QuestionCreateUpdateDto dto, ModelStateDictionary modelState, string prefix);
    Task ReplaceChoicesAsync(Question question, IEnumerable<ChoiceCreateUpdateDto> choices);
    Task RemoveChoicesAsync(Guid questionId);
}
