using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using AppRoles = AAExamManagementSystem.Models.Entities.Roles;

namespace AAExamManagementSystem.Tests;

public class RoleAssignmentServiceTests
{
    private sealed class Fixture
    {
        public ApplicationDbContext Context { get; } = TestSupport.CreateContext();
        public Mock<UserManager<ApplicationUser>> UserManager { get; } = TestSupport.CreateUserManager();
        public Mock<RoleManager<ApplicationRole>> RoleManager { get; } =
            new(Mock.Of<IRoleStore<ApplicationRole>>(), null!, null!, null!, null!);
        public RoleAssignmentService Service { get; }

        public Fixture()
        {
            Service = new RoleAssignmentService(UserManager.Object, RoleManager.Object, Context);
        }

        public ApplicationUser AddUser(string id, int? sectionId = 1)
        {
            var user = new ApplicationUser { Id = id, UserName = id + "@example.com", FirstName = "First", LastName = id, SectionId = sectionId };
            UserManager.Setup(m => m.FindByIdAsync(id)).ReturnsAsync(user);
            return user;
        }

        public ApplicationRole AddRole(string id, string name)
        {
            var role = new ApplicationRole(name) { Id = id };
            RoleManager.Setup(m => m.FindByIdAsync(id)).ReturnsAsync(role);
            return role;
        }

        public async Task AssignAsync(ApplicationUser user, ApplicationRole role, bool isActive = true)
        {
            Context.UserRoles.Add(new ApplicationUserRole { UserId = user.Id, RoleId = role.Id, IsActive = isActive });
            await Context.SaveChangesAsync();
            UserManager.Setup(m => m.IsInRoleAsync(user, role.Name!)).ReturnsAsync(isActive);
        }

        public Task<ApplicationUserRole> RowAsync(string userId, string roleId) =>
            Context.UserRoles.IgnoreQueryFilters().SingleAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
    }

    [Fact]
    public async Task Create_AddsTheAssignment()
    {
        var f = new Fixture();
        var user = f.AddUser("u1");
        var role = f.AddRole("r1", AppRoles.Instructor);

        var result = await f.Service.CreateAsync(new RoleAssignmentCreateDto { UserId = "u1", RoleId = "r1" });

        Assert.True(result.Succeeded);
        Assert.Equal(RoleAssignmentId.Combine("u1", "r1"), result.Value!.Id);
        Assert.True((await f.RowAsync(user.Id, role.Id)).IsActive);
    }

    [Fact]
    public async Task Create_ReactivatesAnInactiveAssignmentInsteadOfAddingASecondRow()
    {
        var f = new Fixture();
        var user = f.AddUser("u1");
        var role = f.AddRole("r1", AppRoles.Instructor);
        await f.AssignAsync(user, role, isActive: false);

        var result = await f.Service.CreateAsync(new RoleAssignmentCreateDto { UserId = "u1", RoleId = "r1" });

        Assert.True(result.Succeeded);
        Assert.Equal(1, await f.Context.UserRoles.IgnoreQueryFilters().CountAsync());
        Assert.True((await f.RowAsync("u1", "r1")).IsActive);
    }

    [Fact]
    public async Task Create_RejectsAnUnknownUserOrRole()
    {
        var f = new Fixture();
        f.AddUser("u1");
        f.AddRole("r1", AppRoles.Instructor);

        var noUser = await f.Service.CreateAsync(new RoleAssignmentCreateDto { UserId = "missing", RoleId = "r1" });
        var noRole = await f.Service.CreateAsync(new RoleAssignmentCreateDto { UserId = "u1", RoleId = "missing" });

        Assert.Equal(ServiceStatus.Invalid, noUser.Status);
        Assert.Equal(ServiceStatus.Invalid, noRole.Status);
        Assert.Empty(f.Context.UserRoles);
    }

    [Fact]
    public async Task Create_RejectsAUserWhoAlreadyHoldsTheRole()
    {
        var f = new Fixture();
        var user = f.AddUser("u1");
        var role = f.AddRole("r1", AppRoles.Instructor);
        await f.AssignAsync(user, role);

        var result = await f.Service.CreateAsync(new RoleAssignmentCreateDto { UserId = "u1", RoleId = "r1" });

        Assert.Equal(ServiceStatus.Invalid, result.Status);
        Assert.Contains("already assigned", result.Error);
    }

    [Fact]
    public async Task Create_RejectsADepartmentRoleForAUserWithoutADepartment()
    {
        var f = new Fixture();
        f.AddUser("u1", sectionId: null);
        f.AddRole("r1", AppRoles.Editor);

        var result = await f.Service.CreateAsync(new RoleAssignmentCreateDto { UserId = "u1", RoleId = "r1" });

        Assert.Equal(ServiceStatus.Invalid, result.Status);
        Assert.Contains("department", result.Error);
        Assert.Empty(f.Context.UserRoles);
    }

    [Fact]
    public async Task ChangeRole_DeactivatesTheOldRoleAndActivatesTheNewOne()
    {
        var f = new Fixture();
        var user = f.AddUser("u1");
        var oldRole = f.AddRole("r1", AppRoles.Instructor);
        f.AddRole("r2", AppRoles.Staffer);
        await f.AssignAsync(user, oldRole);

        var result = await f.Service.ChangeRoleAsync(RoleAssignmentId.Combine("u1", "r1"), "r2", actingUserId: "someone-else");

        Assert.True(result.Succeeded);
        Assert.Equal("r2", result.Value!.RoleId);
        Assert.False((await f.RowAsync("u1", "r1")).IsActive);
        Assert.True((await f.RowAsync("u1", "r2")).IsActive);
    }

    [Fact]
    public async Task ChangeRole_ToTheSameRoleChangesNothing()
    {
        var f = new Fixture();
        var user = f.AddUser("u1");
        var role = f.AddRole("r1", AppRoles.Instructor);
        await f.AssignAsync(user, role);

        var result = await f.Service.ChangeRoleAsync(RoleAssignmentId.Combine("u1", "r1"), "r1", actingUserId: null);

        Assert.True(result.Succeeded);
        Assert.True((await f.RowAsync("u1", "r1")).IsActive);
    }

    [Fact]
    public async Task ChangeRole_BlocksAnAdminMovingThemselvesOffAdmin()
    {
        var f = new Fixture();
        var user = f.AddUser("u1");
        var admin = f.AddRole("r1", AppRoles.Admin);
        f.AddRole("r2", AppRoles.Staffer);
        await f.AssignAsync(user, admin);

        var result = await f.Service.ChangeRoleAsync(RoleAssignmentId.Combine("u1", "r1"), "r2", actingUserId: "u1");

        Assert.Equal(ServiceStatus.Forbidden, result.Status);
        Assert.True((await f.RowAsync("u1", "r1")).IsActive);
        Assert.False(await f.Context.UserRoles.IgnoreQueryFilters().AnyAsync(ur => ur.RoleId == "r2"));
    }

    [Fact]
    public async Task ChangeRole_AllowsAnotherAdminToMoveSomeoneOffAdmin()
    {
        var f = new Fixture();
        var user = f.AddUser("u1");
        var admin = f.AddRole("r1", AppRoles.Admin);
        f.AddRole("r2", AppRoles.Staffer);
        await f.AssignAsync(user, admin);

        var result = await f.Service.ChangeRoleAsync(RoleAssignmentId.Combine("u1", "r1"), "r2", actingUserId: "u2");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ChangeRole_AppliesTheDepartmentRule()
    {
        var f = new Fixture();
        var user = f.AddUser("u1", sectionId: null);
        var oldRole = f.AddRole("r1", AppRoles.Instructor);
        f.AddRole("r2", AppRoles.Editor);
        await f.AssignAsync(user, oldRole);

        var result = await f.Service.ChangeRoleAsync(RoleAssignmentId.Combine("u1", "r1"), "r2", actingUserId: null);

        Assert.Equal(ServiceStatus.Invalid, result.Status);
        Assert.True((await f.RowAsync("u1", "r1")).IsActive);
    }

    [Fact]
    public async Task ChangeRole_ReportsAMalformedIdAsNotFound()
    {
        var f = new Fixture();

        var result = await f.Service.ChangeRoleAsync("not-an-assignment-id", "r2", actingUserId: null);

        Assert.Equal(ServiceStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Deactivate_MarksTheAssignmentInactiveAndKeepsTheRow()
    {
        var f = new Fixture();
        var user = f.AddUser("u1");
        var role = f.AddRole("r1", AppRoles.Instructor);
        await f.AssignAsync(user, role);

        var result = await f.Service.DeactivateAsync(RoleAssignmentId.Combine("u1", "r1"), actingUserId: "u2");

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.IsActive);
        Assert.False((await f.RowAsync("u1", "r1")).IsActive);
    }

    [Fact]
    public async Task Deactivate_BlocksAnAdminDeactivatingTheirOwnAdminAssignment()
    {
        var f = new Fixture();
        var user = f.AddUser("u1");
        var admin = f.AddRole("r1", AppRoles.Admin);
        await f.AssignAsync(user, admin);

        var result = await f.Service.DeactivateAsync(RoleAssignmentId.Combine("u1", "r1"), actingUserId: "u1");

        Assert.Equal(ServiceStatus.Forbidden, result.Status);
        Assert.True((await f.RowAsync("u1", "r1")).IsActive);
    }

    [Fact]
    public async Task Deactivate_ReportsAMissingOrAlreadyInactiveAssignmentAsNotFound()
    {
        var f = new Fixture();
        var user = f.AddUser("u1");
        var role = f.AddRole("r1", AppRoles.Instructor);
        f.AddRole("r2", AppRoles.Staffer);
        await f.AssignAsync(user, role, isActive: false);

        var inactive = await f.Service.DeactivateAsync(RoleAssignmentId.Combine("u1", "r1"), actingUserId: null);
        var missing = await f.Service.DeactivateAsync(RoleAssignmentId.Combine("u1", "r2"), actingUserId: null);

        Assert.Equal(ServiceStatus.NotFound, inactive.Status);
        Assert.Equal(ServiceStatus.NotFound, missing.Status);
    }

    [Fact]
    public async Task Activate_ReactivatesAnInactiveAssignment()
    {
        var f = new Fixture();
        var user = f.AddUser("u1");
        var role = f.AddRole("r1", AppRoles.Instructor);
        await f.AssignAsync(user, role, isActive: false);

        var result = await f.Service.ActivateAsync(RoleAssignmentId.Combine("u1", "r1"));

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.IsActive);
        Assert.True((await f.RowAsync("u1", "r1")).IsActive);
    }

    [Fact]
    public async Task Activate_AppliesTheDepartmentRuleLikeCreateDoes()
    {
        var f = new Fixture();
        var user = f.AddUser("u1", sectionId: null);
        var role = f.AddRole("r1", AppRoles.Editor);
        await f.AssignAsync(user, role, isActive: false);

        var result = await f.Service.ActivateAsync(RoleAssignmentId.Combine("u1", "r1"));

        Assert.Equal(ServiceStatus.Invalid, result.Status);
        Assert.False((await f.RowAsync("u1", "r1")).IsActive);
    }

    [Fact]
    public async Task Activate_DoesNotInventAnAssignmentThatNeverExisted()
    {
        var f = new Fixture();
        f.AddUser("u1");
        f.AddRole("r1", AppRoles.Instructor);

        var result = await f.Service.ActivateAsync(RoleAssignmentId.Combine("u1", "r1"));

        Assert.Equal(ServiceStatus.NotFound, result.Status);
        Assert.Empty(f.Context.UserRoles);
    }

    [Fact]
    public async Task GetById_ReturnsInactiveAssignmentsAndNullForUnknownOnes()
    {
        var f = new Fixture();
        var user = f.AddUser("u1");
        var role = f.AddRole("r1", AppRoles.Instructor);
        await f.AssignAsync(user, role, isActive: false);

        var found = await f.Service.GetByIdAsync(RoleAssignmentId.Combine("u1", "r1"));
        var malformed = await f.Service.GetByIdAsync("nope");

        Assert.NotNull(found);
        Assert.False(found!.IsActive);
        Assert.Equal("First u1", found.UserName);
        Assert.Null(malformed);
    }

    [Fact]
    public async Task GetById_FallsBackToTheLoginNameWhenTheUserHasNoName()
    {
        var f = new Fixture();
        var user = f.AddUser("u1");
        user.FirstName = string.Empty;
        user.LastName = string.Empty;
        var role = f.AddRole("r1", AppRoles.Instructor);
        await f.AssignAsync(user, role);

        var found = await f.Service.GetByIdAsync(RoleAssignmentId.Combine("u1", "r1"));

        Assert.Equal("u1@example.com", found!.UserName);
    }
}
