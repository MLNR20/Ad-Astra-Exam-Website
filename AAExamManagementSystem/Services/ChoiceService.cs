using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AutoMapper;

namespace AAExamManagementSystem.Services;

public class ChoiceService : IChoiceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ChoiceService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IEnumerable<ChoiceDto>> GetAllAsync()
    {
        var choices = await _unitOfWork.Choices.GetAllAsync();
        return _mapper.Map<IList<ChoiceDto>>(choices);
    }

    public async Task<ChoiceDto?> GetByIdAsync(Guid id)
    {
        var choice = await _unitOfWork.Choices.GetByIdAsync(id);
        return choice is null ? null : _mapper.Map<ChoiceDto>(choice);
    }

    public async Task<ChoiceDto> CreateAsync(ChoiceCreateUpdateDto dto)
    {
        var choice = _mapper.Map<Choice>(dto);
        await _unitOfWork.Choices.AddAsync(choice);
        await _unitOfWork.SaveChangesAsync();
        return _mapper.Map<ChoiceDto>(choice);
    }

    public async Task<ServiceResult> UpdateAsync(Guid id, ChoiceCreateUpdateDto dto)
    {
        var choice = await _unitOfWork.Choices.GetByIdAsync(id);
        if (choice is null) return ServiceResult.NotFound();

        choice.ChoiceText = dto.ChoiceText;
        choice.IsCorrect = dto.IsCorrect;
        choice.IsActive = dto.IsActive;
        choice.DateUpdated = DateTime.UtcNow;
        _unitOfWork.Choices.Update(choice);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(Guid id)
    {
        var choice = await _unitOfWork.Choices.GetByIdAsync(id);
        if (choice is null) return ServiceResult.NotFound();

        _unitOfWork.Choices.Remove(choice);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult.Success();
    }
}
