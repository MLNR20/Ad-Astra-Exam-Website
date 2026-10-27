using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using AutoMapper;
using Moq;

namespace AAExamManagementSystem.Tests;

public class DepartmentServiceTests
{
    private static (DepartmentService Service, Mock<IUnitOfWork> UnitOfWork, Mock<IGenericRepository<Department>> Departments) CreateService()
    {
        var departments = new Mock<IGenericRepository<Department>>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Departments).Returns(departments.Object);
        return (new DepartmentService(unitOfWork.Object, CreateMapper()), unitOfWork, departments);
    }

    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(
            cfg =>
            {
                cfg.CreateMap<Department, DepartmentDto>();
                cfg.CreateMap<DepartmentCreateUpdateDto, Department>();
            },
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    }

    [Fact]
    public async Task GetAll_ReturnsAllDepartments()
    {
        var (service, _, departments) = CreateService();
        departments.Setup(r => r.GetAllIncludingInactiveAsync()).ReturnsAsync(new[]
        {
            new Department { Id = "dept-1", Name = "Department A" },
            new Department { Id = "dept-2", Name = "Department B" },
        });

        var result = await service.GetAllAsync();

        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task GetById_WithExistingId_ReturnsDepartment()
    {
        var (service, _, departments) = CreateService();
        departments.Setup(r => r.GetByIdAsync("dept-1")).ReturnsAsync(new Department { Id = "dept-1", Name = "Department A" });

        var result = await service.GetByIdAsync("dept-1");

        Assert.Equal("Department A", result!.Name);
    }

    [Fact]
    public async Task GetById_WithMissingId_ReturnsNull()
    {
        var (service, _, departments) = CreateService();
        departments.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((Department?)null);

        Assert.Null(await service.GetByIdAsync("missing"));
    }

    [Fact]
    public async Task Create_AddsDepartmentAndSaves()
    {
        var (service, unitOfWork, departments) = CreateService();

        var result = await service.CreateAsync(new DepartmentCreateUpdateDto { Name = "New Department", IsActive = true });

        Assert.Equal("New Department", result.Name);
        departments.Verify(r => r.AddAsync(It.Is<Department>(d => d.Name == "New Department")), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_WithExistingId_UpdatesDepartment()
    {
        var (service, unitOfWork, departments) = CreateService();
        var department = new Department { Id = "dept-1", Name = "Old Name", IsActive = true };
        departments.Setup(r => r.GetByIdAsync("dept-1")).ReturnsAsync(department);

        var result = await service.UpdateAsync("dept-1", new DepartmentCreateUpdateDto { Name = "New Name", IsActive = false });

        Assert.True(result.Succeeded);
        Assert.Equal("New Name", department.Name);
        Assert.False(department.IsActive);
        departments.Verify(r => r.Update(department), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_WithMissingId_ReturnsNotFound()
    {
        var (service, unitOfWork, departments) = CreateService();
        departments.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((Department?)null);

        var result = await service.UpdateAsync("missing", new DepartmentCreateUpdateDto { Name = "New Name" });

        Assert.Equal(ServiceStatus.NotFound, result.Status);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Delete_WithExistingId_SoftDeletesDepartment()
    {
        var (service, unitOfWork, departments) = CreateService();
        var department = new Department { Id = "dept-1", Name = "Department A", IsActive = true };
        departments.Setup(r => r.GetByIdAsync("dept-1")).ReturnsAsync(department);

        var result = await service.DeleteAsync("dept-1");

        Assert.True(result.Succeeded);
        Assert.False(department.IsActive);
        departments.Verify(r => r.Update(department), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Delete_WithMissingId_ReturnsNotFound()
    {
        var (service, unitOfWork, departments) = CreateService();
        departments.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((Department?)null);

        var result = await service.DeleteAsync("missing");

        Assert.Equal(ServiceStatus.NotFound, result.Status);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }
}
