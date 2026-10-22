using AAExamManagementSystem.Controllers;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AAExamManagementSystem.Tests;

public class RolesControllerTests
{
    private static (Mock<IUnitOfWork> UnitOfWork, Mock<IRoleRepository> Roles) CreateUnitOfWork()
    {
        var roles = new Mock<IRoleRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Roles).Returns(roles.Object);
        return (unitOfWork, roles);
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
        var (unitOfWork, roles) = CreateUnitOfWork();
        roles.Setup(r => r.GetAllAsync()).ReturnsAsync(new[]
        {
            new ApplicationRole("Admin"),
            new ApplicationRole("Instructor"),
        });

        var controller = new RolesController(unitOfWork.Object, CreateMapper());

        var result = await controller.GetAll();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<RoleDto>>(okResult.Value);
        Assert.Equal(2, dtos.Count());
    }

    [Fact]
    public async Task GetById_WithMissingId_ReturnsNotFound()
    {
        var (unitOfWork, roles) = CreateUnitOfWork();
        roles.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((ApplicationRole?)null);

        var controller = new RolesController(unitOfWork.Object, CreateMapper());

        var result = await controller.GetById("missing");

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Create_WithDuplicateRoleName_ReturnsBadRequest_AndDoesNotSave()
    {
        var (unitOfWork, roles) = CreateUnitOfWork();
        roles.Setup(r => r.NameExistsAsync("Admin")).ReturnsAsync(true);

        var controller = new RolesController(unitOfWork.Object, CreateMapper());

        var result = await controller.Create(new RoleCreateUpdateDto { Name = "Admin" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        roles.Verify(r => r.AddAsync(It.IsAny<ApplicationRole>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Create_WithUniqueRoleName_CreatesRole()
    {
        var (unitOfWork, roles) = CreateUnitOfWork();
        roles.Setup(r => r.NameExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        var controller = new RolesController(unitOfWork.Object, CreateMapper());

        var result = await controller.Create(new RoleCreateUpdateDto { Name = "Grader" });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<RoleDto>(created.Value);
        Assert.Equal("Grader", dto.Name);
        roles.Verify(r => r.AddAsync(It.Is<ApplicationRole>(role => role.Name == "Grader")), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Update_ChangingToExistingRoleName_ReturnsBadRequest()
    {
        var (unitOfWork, roles) = CreateUnitOfWork();
        var existingRole = new ApplicationRole("Instructor") { Id = "role-1" };
        roles.Setup(r => r.GetByIdAsync("role-1")).ReturnsAsync(existingRole);
        roles.Setup(r => r.NameExistsAsync("Admin")).ReturnsAsync(true);

        var controller = new RolesController(unitOfWork.Object, CreateMapper());

        var result = await controller.Update("role-1", new RoleCreateUpdateDto { Name = "Admin" });

        Assert.IsType<BadRequestObjectResult>(result);
        roles.Verify(r => r.Update(It.IsAny<ApplicationRole>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Update_WithExistingId_UpdatesRoleAndReturnsNoContent()
    {
        var (unitOfWork, roles) = CreateUnitOfWork();
        var role = new ApplicationRole("Instructor") { Id = "role-1" };
        roles.Setup(r => r.GetByIdAsync("role-1")).ReturnsAsync(role);
        roles.Setup(r => r.NameExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        var controller = new RolesController(unitOfWork.Object, CreateMapper());

        var result = await controller.Update("role-1", new RoleCreateUpdateDto { Name = "Grader", IsActive = false });

        Assert.IsType<NoContentResult>(result);
        Assert.Equal("Grader", role.Name);
        Assert.False(role.IsActive);
        roles.Verify(r => r.Update(role), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Delete_WithExistingId_SoftDeletesRoleAndReturnsNoContent()
    {
        var (unitOfWork, roles) = CreateUnitOfWork();
        var role = new ApplicationRole("Instructor") { Id = "role-1" };
        roles.Setup(r => r.GetByIdAsync("role-1")).ReturnsAsync(role);

        var controller = new RolesController(unitOfWork.Object, CreateMapper());

        var result = await controller.Delete("role-1");

        Assert.IsType<NoContentResult>(result);
        Assert.False(role.IsActive);
        roles.Verify(r => r.Update(role), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Delete_WithMissingId_ReturnsNotFound()
    {
        var (unitOfWork, roles) = CreateUnitOfWork();
        roles.Setup(r => r.GetByIdAsync(It.IsAny<object>())).ReturnsAsync((ApplicationRole?)null);

        var controller = new RolesController(unitOfWork.Object, CreateMapper());

        var result = await controller.Delete("missing");

        Assert.IsType<NotFoundResult>(result);
        unitOfWork.Verify(u => u.SaveChangesAsync(), Times.Never);
    }
}
