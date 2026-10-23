using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AutoMapper;

namespace AAExamManagementSystem.Services;

public class CourseService : ICourseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CourseService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<IEnumerable<CourseDto>> GetAllAsync()
    {
        var courses = await _unitOfWork.Courses.GetAllAsync();
        return _mapper.Map<IEnumerable<CourseDto>>(courses);
    }

    public async Task<CourseDto?> GetByIdAsync(int id)
    {
        var course = await _unitOfWork.Courses.GetByIdAsync(id);
        return course is null ? null : _mapper.Map<CourseDto>(course);
    }

    public async Task<CourseDto> CreateAsync(CourseCreateUpdateDto dto)
    {
        var course = _mapper.Map<Course>(dto);
        await _unitOfWork.Courses.AddAsync(course);
        await _unitOfWork.SaveChangesAsync();
        return _mapper.Map<CourseDto>(course);
    }

    public async Task<ServiceResult> UpdateAsync(int id, CourseCreateUpdateDto dto)
    {
        var course = await _unitOfWork.Courses.GetByIdAsync(id);
        if (course is null) return ServiceResult.NotFound();

        course.Name = dto.Name;
        course.IsActive = dto.IsActive;
        course.DateUpdated = DateTime.UtcNow;
        _unitOfWork.Courses.Update(course);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var course = await _unitOfWork.Courses.GetByIdAsync(id);
        if (course is null) return ServiceResult.NotFound();

        course.IsActive = false;
        course.DateUpdated = DateTime.UtcNow;
        _unitOfWork.Courses.Update(course);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult.Success();
    }
}
