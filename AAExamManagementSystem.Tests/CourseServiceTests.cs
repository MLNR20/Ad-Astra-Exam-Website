using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using AutoMapper;
using Moq;

namespace AAExamManagementSystem.Tests;

public class CourseServiceTests
{
    private static (CourseService Service, Mock<IUnitOfWork> UnitOfWork, Mock<IGenericRepository<Course>> Courses) CreateService()
    {
        var courses = new Mock<IGenericRepository<Course>>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Courses).Returns(courses.Object);
        return (new CourseService(unitOfWork.Object, CreateMapper()), unitOfWork, courses);
    }

    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(
            cfg =>
            {
                cfg.CreateMap<Course, CourseDto>();
                cfg.CreateMap<CourseCreateUpdateDto, Course>();
            },
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    }

    [Fact]
    public async Task GetAll_ReturnsAllCourses()
    {
        var (service, _, courses) = CreateService();
        courses.Setup(r => r.GetAllIncludingInactiveAsync()).ReturnsAsync(new[]
        {
            new Course { Id = 1, Name = "Course A" },
            new Course { Id = 2, Name = "Course B" },
        });

        Assert.Equal(2, (await service.GetAllAsync()).Count());
    }

    [Fact]
    public async Task GetById_WithMissingId_ReturnsNull()
    {
        var (service, _, courses) = CreateService();
        courses.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((Course?)null);

        Assert.Null(await service.GetByIdAsync(99));
    }

    [Fact]
    public async Task Create_AddsCourseAndSaves()
    {
        var (service, unitOfWork, courses) = CreateService();

        var result = await service.CreateAsync(new CourseCreateUpdateDto { Name = "New Course" });

        Assert.Equal("New Course", result.Name);
        courses.Verify(r => r.AddAsync(It.Is<Course>(c => c.Name == "New Course")), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_WithExistingId_UpdatesCourse()
    {
        var (service, unitOfWork, courses) = CreateService();
        var course = new Course { Id = 1, Name = "Old", IsActive = true };
        courses.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(course);

        var result = await service.UpdateAsync(1, new CourseCreateUpdateDto { Name = "New", IsActive = false });

        Assert.True(result.Succeeded);
        Assert.Equal("New", course.Name);
        Assert.False(course.IsActive);
        courses.Verify(r => r.Update(course), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_WithMissingId_ReturnsNotFound()
    {
        var (service, unitOfWork, courses) = CreateService();
        courses.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((Course?)null);

        var result = await service.UpdateAsync(99, new CourseCreateUpdateDto { Name = "New" });

        Assert.Equal(ServiceStatus.NotFound, result.Status);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Delete_WithExistingId_SoftDeletesCourse()
    {
        var (service, unitOfWork, courses) = CreateService();
        var course = new Course { Id = 1, Name = "Course A", IsActive = true };
        courses.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(course);

        var result = await service.DeleteAsync(1);

        Assert.True(result.Succeeded);
        Assert.False(course.IsActive);
        courses.Verify(r => r.Update(course), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Delete_WithMissingId_ReturnsNotFound()
    {
        var (service, unitOfWork, courses) = CreateService();
        courses.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((Course?)null);

        var result = await service.DeleteAsync(99);

        Assert.Equal(ServiceStatus.NotFound, result.Status);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }
}
