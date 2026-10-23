using AAExamManagementSystem.Models.Dtos;

namespace AAExamManagementSystem.Services;

public interface ICourseService
{
    Task<IEnumerable<CourseDto>> GetAllAsync();
    Task<CourseDto?> GetByIdAsync(int id);
    Task<CourseDto> CreateAsync(CourseCreateUpdateDto dto);
    Task<ServiceResult> UpdateAsync(int id, CourseCreateUpdateDto dto);
    Task<ServiceResult> DeleteAsync(int id);
}
