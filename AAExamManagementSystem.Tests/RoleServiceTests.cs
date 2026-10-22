using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using AutoMapper;
using Moq;

namespace AAExamManagementSystem.Tests;

public class RoleServiceTests
{
    private static (RoleService Service, Mock<IUnitOfWork> UnitOfWork, Mock<IRoleRepository> Roles) CreateService()
    {
        var roles = new Mock<IRoleRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Roles).Returns(roles.Object);
        return (new RoleService(unitOfWork.Object, CreateMapper()), unitOfWork, roles);
    }

    private static IMapper CreateMapper()
    {
        var configuration = new MapperConfiguration(
            cfg => cfg.CreateMap<ApplicationRole, RoleDto>(),
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        return configuration.CreateMapper();
    }

    [Fact]
    public async Task GetAll_ReturnsAllRoles()
    {
        var (service, _, roles) = CreateService();
        roles.Setup(r => r.GetAllAsync()).ReturnsAsync(new[]
        {
            new ApplicationRole("Admin"),
            new ApplicationRole("Instructor"),
        });

        var result = await service.GetAllAsync();

        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task GetById_WithMissingId_ReturnsNull()
    {
        var (service, _, roles) = CreateService();
        roles.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((ApplicationRole?)null);

        Assert.Null(await service.GetByIdAsync("missing"));
    }

    [Fact]
    public async Task Create_WithDuplicateRoleName_ReturnsInvalid_AndDoesNotSave()
    {
        var (service, unitOfWork, roles) = CreateService();
        roles.Setup(r => r.NameExistsAsync("Admin")).ReturnsAsync(true);

        var result = await service.CreateAsync(new RoleCreateUpdateDto { Name = "Admin" });

        Assert.Equal(ServiceStatus.Invalid, result.Status);
        roles.Verify(r => r.AddAsync(It.IsAny<ApplicationRole>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Create_WithUniqueRoleName_CreatesRole()
    {
        var (service, unitOfWork, roles) = CreateService();
        roles.Setup(r => r.NameExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        var result = await service.CreateAsync(new RoleCreateUpdateDto { Name = "Grader" });

        Assert.True(result.Succeeded);
        Assert.Equal("Grader", result.Value!.Name);
        roles.Verify(r => r.AddAsync(It.Is<ApplicationRole>(role => role.Name == "Grader")), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_ChangingToExistingRoleName_ReturnsInvalid()
    {
        var (service, unitOfWork, roles) = CreateService();
        roles.Setup(r => r.GetByIdAsync("role-1")).ReturnsAsync(new ApplicationRole("Instructor") { Id = "role-1" });
        roles.Setup(r => r.NameExistsAsync("Admin")).ReturnsAsync(true);

        var result = await service.UpdateAsync("role-1", new RoleCreateUpdateDto { Name = "Admin" });

        Assert.Equal(ServiceStatus.Invalid, result.Status);
        roles.Verify(r => r.Update(It.IsAny<ApplicationRole>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Update_WithExistingId_UpdatesRole()
    {
        var (service, unitOfWork, roles) = CreateService();
        var role = new ApplicationRole("Instructor") { Id = "role-1" };
        roles.Setup(r => r.GetByIdAsync("role-1")).ReturnsAsync(role);
        roles.Setup(r => r.NameExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        var result = await service.UpdateAsync("role-1", new RoleCreateUpdateDto { Name = "Grader", IsActive = false });

        Assert.True(result.Succeeded);
        Assert.Equal("Grader", role.Name);
        Assert.False(role.IsActive);
        roles.Verify(r => r.Update(role), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_WithMissingId_ReturnsNotFound()
    {
        var (service, unitOfWork, roles) = CreateService();
        roles.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((ApplicationRole?)null);

        var result = await service.UpdateAsync("missing", new RoleCreateUpdateDto { Name = "Grader" });

        Assert.Equal(ServiceStatus.NotFound, result.Status);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Delete_WithExistingId_SoftDeletesRole()
    {
        var (service, unitOfWork, roles) = CreateService();
        var role = new ApplicationRole("Instructor") { Id = "role-1" };
        roles.Setup(r => r.GetByIdAsync("role-1")).ReturnsAsync(role);

        var result = await service.DeleteAsync("role-1");

        Assert.True(result.Succeeded);
        Assert.False(role.IsActive);
        roles.Verify(r => r.Update(role), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Delete_WithMissingId_ReturnsNotFound()
    {
        var (service, unitOfWork, roles) = CreateService();
        roles.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((ApplicationRole?)null);

        var result = await service.DeleteAsync("missing");

        Assert.Equal(ServiceStatus.NotFound, result.Status);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }
}
