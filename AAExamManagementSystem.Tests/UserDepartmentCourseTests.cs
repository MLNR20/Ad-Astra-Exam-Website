using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Pages.Users;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;

namespace AAExamManagementSystem.Tests;

public class UserDepartmentCourseTests
{
    private sealed class Fixture
    {
        public ApplicationDbContext Db { get; } = TestSupport.CreateContext();
        public Mock<UserManager<ApplicationUser>> Users { get; } = TestSupport.CreateUserManager();
        public Department Academics { get; } = new() { Name = "Academics" };
        public Department Closed { get; } = new() { Name = "Closed", IsActive = false };
        public Course Networking { get; } = new() { Name = "Networking" };
        public Course Retired { get; } = new() { Name = "Retired", IsActive = false };
        public ApplicationUser? Saved { get; private set; }

        public Fixture()
        {
            Db.Departments.AddRange(Academics, Closed);
            Db.Courses.AddRange(Networking, Retired);
            Db.SaveChanges();
        }

        private static Mock<RoleManager<ApplicationRole>> Roles()
        {
            var roles = new Mock<RoleManager<ApplicationRole>>(Mock.Of<IRoleStore<ApplicationRole>>(), null!, null!, null!, null!);
            roles.Setup(r => r.Roles).Returns(new List<ApplicationRole>().AsQueryable());
            return roles;
        }

        public CreateModel CreatePage(string? departmentId, int? courseId)
        {
            Users.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .Callback<ApplicationUser, string>((u, _) => Saved = u)
                .ReturnsAsync(IdentityResult.Success);
            Users.Setup(m => m.AddToRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(IdentityResult.Success);

            var model = new CreateModel(
                Users.Object,
                Roles().Object,
                new GenericRepository<Section>(Db),
                new GenericRepository<Department>(Db),
                new GenericRepository<Course>(Db),
                new ActiveIdentityValidator(Db))
            {
                NewUser = new UserCreateDto
                {
                    UserName = "newstaff",
                    FirstName = "New",
                    LastName = "Staff",
                    Email = "new@aaexams.local",
                    Password = "P@ssword2026!",
                    ConfirmPassword = "P@ssword2026!",
                    DepartmentId = departmentId,
                    CourseId = courseId,
                },
            };
            TestSupport.Attach(model, TestSupport.PrincipalFor(AAExamManagementSystem.Models.Entities.Roles.Admin));
            return model;
        }

        public EditModel EditPage(ApplicationUser existing, string? departmentId, int? courseId)
        {
            Users.Setup(m => m.FindByIdAsync(existing.Id)).ReturnsAsync(existing);
            Users.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
                .Callback<ApplicationUser>(u => Saved = u)
                .ReturnsAsync(IdentityResult.Success);
            Users.Setup(m => m.GetRolesAsync(existing)).ReturnsAsync(new List<string>());

            var model = new EditModel(
                Users.Object,
                Roles().Object,
                new GenericRepository<Section>(Db),
                new GenericRepository<Department>(Db),
                new GenericRepository<Course>(Db),
                new ActiveIdentityValidator(Db))
            {
                Id = existing.Id,
                TargetUser = new UserEditDto
                {
                    FirstName = existing.FirstName,
                    LastName = existing.LastName,
                    Email = existing.Email!,
                    DepartmentId = departmentId,
                    CourseId = courseId,
                },
            };
            TestSupport.Attach(model, TestSupport.PrincipalFor(AAExamManagementSystem.Models.Entities.Roles.Admin));
            return model;
        }
    }

    private static ApplicationUser ExistingUser(string? departmentId = null, int? courseId = null) => new()
    {
        Id = "user-1",
        UserName = "staff",
        Email = "staff@aaexams.local",
        FirstName = "Staff",
        LastName = "Member",
        DepartmentId = departmentId,
        CourseId = courseId,
    };

    [Fact]
    public async Task Creating_AssignsTheChosenDepartmentAndCourse()
    {
        var f = new Fixture();

        var result = await f.CreatePage(f.Academics.Id, f.Networking.Id).OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(f.Academics.Id, f.Saved!.DepartmentId);
        Assert.Equal(f.Networking.Id, f.Saved.CourseId);
    }

    [Fact]
    public async Task Creating_RejectsAnInactiveOrUnknownDepartmentAndCourse()
    {
        var f = new Fixture();
        var page = f.CreatePage(f.Closed.Id, f.Retired.Id);

        var result = await page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(page.ModelState.ContainsKey("NewUser.DepartmentId"));
        Assert.True(page.ModelState.ContainsKey("NewUser.CourseId"));
        Assert.Null(f.Saved);
    }

    [Fact]
    public async Task Editing_ChangesAndClearsTheDepartmentAndCourse()
    {
        var f = new Fixture();

        await f.EditPage(ExistingUser(), f.Academics.Id, f.Networking.Id).OnPostAsync();
        Assert.Equal(f.Academics.Id, f.Saved!.DepartmentId);
        Assert.Equal(f.Networking.Id, f.Saved.CourseId);

        await f.EditPage(f.Saved, null, null).OnPostAsync();
        Assert.Null(f.Saved.DepartmentId);
        Assert.Null(f.Saved.CourseId);
    }

    [Fact]
    public async Task Editing_LetsAUserKeepADepartmentAndCourseThatWereDeactivatedLater()
    {
        var f = new Fixture();
        var existing = ExistingUser(f.Closed.Id, f.Retired.Id);
        var page = f.EditPage(existing, f.Closed.Id, f.Retired.Id);

        var result = await page.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(f.Closed.Id, existing.DepartmentId);
        Assert.Equal(f.Retired.Id, existing.CourseId);
    }

    [Fact]
    public async Task Editing_RejectsNewlyChoosingAnInactiveDepartment()
    {
        var f = new Fixture();
        var existing = ExistingUser();
        var page = f.EditPage(existing, f.Closed.Id, null);

        var result = await page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(page.ModelState.ContainsKey("TargetUser.DepartmentId"));
        Assert.Null(existing.DepartmentId);
    }

    [Fact]
    public async Task Editing_OffersActiveChoicesPlusTheUsersCurrentOne()
    {
        var f = new Fixture();
        var page = f.EditPage(ExistingUser(f.Closed.Id, null), null, null);

        await page.OnGetAsync();

        Assert.Equal(f.Closed.Id, page.TargetUser.DepartmentId);
        Assert.Equal(new[] { "Academics", "Closed" }, page.DepartmentOptions.Select(o => o.Text).Order());
        Assert.Equal(new[] { "Networking" }, page.CourseOptions.Select(o => o.Text));
    }
}
