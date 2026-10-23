using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AutoMapper;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AAExamManagementSystem.Services;

public class QuestionService : IQuestionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IQuestionChoiceService _choiceService;
    private readonly IMapper _mapper;

    public QuestionService(IUnitOfWork unitOfWork, IQuestionChoiceService choiceService, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _choiceService = choiceService;
        _mapper = mapper;
    }

    public async Task<IEnumerable<QuestionDto>> GetAllAsync()
    {
        var questions = await _unitOfWork.Questions.GetAllAsync();
        var dtos = _mapper.Map<IList<QuestionDto>>(questions);
        await PopulateLookupsAsync(dtos);
        return dtos;
    }

    public async Task<QuestionDto?> GetByIdAsync(Guid id)
    {
        var question = await _unitOfWork.Questions.GetByIdAsync(id);
        if (question is null) return null;

        var dto = _mapper.Map<QuestionDto>(question);
        await PopulateLookupsAsync(new[] { dto });
        dto.Choices = await _choiceService.GetChoicesAsync(id);
        return dto;
    }

    public async Task<ServiceResult<QuestionDto>> CreateAsync(QuestionCreateUpdateDto dto)
    {
        var modelState = new ModelStateDictionary();
        await _choiceService.NormalizeAndValidateAsync(dto, modelState, string.Empty);
        if (!modelState.IsValid) return ServiceResult<QuestionDto>.ValidationFailed(modelState);

        var question = _mapper.Map<Question>(dto);
        await _unitOfWork.Questions.AddAsync(question);
        await _choiceService.ReplaceChoicesAsync(question, dto.Choices);
        await _unitOfWork.SaveChangesAsync();

        var result = _mapper.Map<QuestionDto>(question);
        await PopulateLookupsAsync(new[] { result });
        result.Choices = await _choiceService.GetChoicesAsync(question.Id);
        return ServiceResult<QuestionDto>.Success(result);
    }

    public async Task<ServiceResult> UpdateAsync(Guid id, QuestionCreateUpdateDto dto)
    {
        var question = await _unitOfWork.Questions.GetByIdAsync(id);
        if (question is null) return ServiceResult.NotFound();

        var modelState = new ModelStateDictionary();
        await _choiceService.NormalizeAndValidateAsync(dto, modelState, string.Empty);
        if (!modelState.IsValid) return ServiceResult.ValidationFailed(modelState);

        question.QuestionTypeId = dto.QuestionTypeId;
        question.SectionId = dto.SectionId;
        question.QuestionTitle = dto.QuestionTitle;
        question.Image = dto.Image;
        question.Score = dto.Score;
        question.IsUpToEvaluation = dto.IsUpToEvaluation;
        question.IsActive = dto.IsActive;
        question.DateUpdated = DateTime.UtcNow;
        _unitOfWork.Questions.Update(question);
        await _choiceService.ReplaceChoicesAsync(question, dto.Choices);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(Guid id)
    {
        var question = await _unitOfWork.Questions.GetByIdAsync(id);
        if (question is null) return ServiceResult.NotFound();

        await _choiceService.RemoveChoicesAsync(question.Id);
        _unitOfWork.Questions.Remove(question);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult.Success();
    }

    private async Task PopulateLookupsAsync(IEnumerable<QuestionDto> dtos)
    {
        var questionTypes = await _unitOfWork.QuestionTypes.GetAllAsync();
        var sections = await _unitOfWork.Sections.GetAllAsync();
        var questionTypeNames = questionTypes.ToDictionary(qt => qt.Id, qt => qt.Name);
        var sectionNames = sections.ToDictionary(s => s.Id, s => s.Name);
        foreach (var dto in dtos)
        {
            dto.QuestionTypeName = questionTypeNames.GetValueOrDefault(dto.QuestionTypeId, string.Empty);
            dto.SectionName = sectionNames.GetValueOrDefault(dto.SectionId, string.Empty);
        }
    }
}
