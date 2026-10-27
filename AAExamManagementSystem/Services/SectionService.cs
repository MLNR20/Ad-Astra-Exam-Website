using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AutoMapper;

namespace AAExamManagementSystem.Services;

public class SectionService : ISectionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public SectionService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IEnumerable<SectionDto>> GetAllAsync()
    {
        var sections = await _unitOfWork.Sections.GetAllIncludingInactiveAsync();
        return _mapper.Map<IEnumerable<SectionDto>>(sections);
    }

    public async Task<SectionDto?> GetByIdAsync(int id)
    {
        var section = await _unitOfWork.Sections.GetByIdAsync(id);
        return section is null ? null : _mapper.Map<SectionDto>(section);
    }

    public async Task<SectionDto> CreateAsync(SectionCreateUpdateDto dto)
    {
        var section = _mapper.Map<Section>(dto);
        await _unitOfWork.Sections.AddAsync(section);
        await _unitOfWork.SaveChangesAsync();
        return _mapper.Map<SectionDto>(section);
    }

    public async Task<ServiceResult> UpdateAsync(int id, SectionCreateUpdateDto dto)
    {
        var section = await _unitOfWork.Sections.GetByIdAsync(id);
        if (section is null) return ServiceResult.NotFound();

        section.Name = dto.Name;
        _unitOfWork.Sections.Update(section);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var section = await _unitOfWork.Sections.GetByIdAsync(id);
        if (section is null) return ServiceResult.NotFound();

        section.IsActive = false;
        _unitOfWork.Sections.Update(section);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult.Success();
    }
}
