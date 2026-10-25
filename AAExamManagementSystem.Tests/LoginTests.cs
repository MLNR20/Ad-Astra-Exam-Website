using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Pages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace AAExamManagementSystem.Tests;

public class LoginTests
{
    private const string UserName = "jdoe";

    private sealed class Fixture
    {
        public Mock<UserManager<ApplicationUser>> Users { get; } = TestSupport.CreateUserManager();
        public Mock<SignInManager<ApplicationUser>> SignIn { get; }
        public AAExamManagementSystem.Repository.ApplicationDbContext Db { get; } = TestSupport.CreateContext();
        public ApplicationUser User { get; } = new() { Id = "u1", UserName = UserName, Email = "jdoe@example.test" };

        public Fixture()
        {
            SignIn = TestSupport.CreateSignInManager(Users.Object);
            Users.Setup(m => m.FindByNameAsync(UserName)).ReturnsAsync(User);
            Users.Setup(m => m.FindByEmailAsync(User.Email!)).ReturnsAsync(User);
        }

        public void SignInReturns(SignInResult result) =>
            SignIn.Setup(s => s.PasswordSignInAsync(UserName, It.IsAny<string>(), It.IsAny<bool>(), true)).ReturnsAsync(result);

        public void UserHasRole(string role, bool hasRole = true) =>
            Users.Setup(m => m.IsInRoleAsync(User, role)).ReturnsAsync(hasRole);

        public LoginModel StaffLogin(string userName = UserName, string password = "secret")
        {
            var model = new LoginModel(SignIn.Object, Users.Object, Db)
            {
                Input = new LoginModel.LoginInput { UserName = userName, Password = password },
            };
            TestSupport.Attach(model, TestSupport.PrincipalFor());
            return model;
        }

        public ApplicantLoginModel ApplicantLogin(string userName = UserName, string password = "secret")
        {
            var model = new ApplicantLoginModel(SignIn.Object, Users.Object, Db)
            {
                Input = new ApplicantLoginModel.LoginInput { UserName = userName, Password = password },
            };
            TestSupport.Attach(model, TestSupport.PrincipalFor());
            return model;
        }

        public Task<List<AuditLog>> AuditLogsAsync() => Db.AuditLogs.ToListAsync();
    }

    [Fact]
    public async Task StaffLogin_WithInvalidModelState_ReturnsPageWithoutLookingUpTheUser()
    {
        var f = new Fixture();
        var model = f.StaffLogin();
        model.ModelState.AddModelError("Input.Password", "Required");

        var result = await model.OnPostAsync();

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
        f.Users.Verify(m => m.FindByNameAsync(It.IsAny<string>()), Times.Never);
        Assert.Empty(await f.AuditLogsAsync());
    }

    [Fact]
    public async Task StaffLogin_WithUnknownUser_FailsWithAGenericMessageAndLogsIt()
    {
        var f = new Fixture();
        f.Users.Setup(m => m.FindByNameAsync("ghost")).ReturnsAsync((ApplicationUser?)null);
        var model = f.StaffLogin("ghost");

        var result = await model.OnPostAsync();

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
        Assert.Contains("Invalid username/email or password.", model.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage));
        var log = Assert.Single(await f.AuditLogsAsync());
        Assert.False(log.IsActive);
        Assert.Null(log.CreatedById);
        Assert.Contains("ghost", log.ResponseBody);
    }

    [Fact]
    public async Task StaffLogin_WithAnEmail_LooksTheUserUpByEmail()
    {
        var f = new Fixture();
        f.SignInReturns(SignInResult.Success);
        f.UserHasRole(AppRoles.Applicant, false);
        var model = f.StaffLogin(f.User.Email!);

        await model.OnPostAsync();

        f.Users.Verify(m => m.FindByEmailAsync(f.User.Email!), Times.Once);
        f.Users.Verify(m => m.FindByNameAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task StaffLogin_WithTheWrongPassword_FailsWithTheSameGenericMessage()
    {
        var f = new Fixture();
        f.SignInReturns(SignInResult.Failed);
        var model = f.StaffLogin();

        var result = await model.OnPostAsync();

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
        Assert.Contains("Invalid username/email or password.", model.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage));
        Assert.False(Assert.Single(await f.AuditLogsAsync()).IsActive);
    }

    [Fact]
    public async Task StaffLogin_WhenLockedOut_ShowsTheLockoutMessage()
    {
        var f = new Fixture();
        f.SignInReturns(SignInResult.LockedOut);
        var model = f.StaffLogin();

        await model.OnPostAsync();

        Assert.Contains("This account has been locked out. Please try again later.",
            model.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage));
        Assert.Contains("locked out", Assert.Single(await f.AuditLogsAsync()).ResponseBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StaffLogin_Succeeds_RedirectsToTheDashboardAndLogsIt()
    {
        var f = new Fixture();
        f.SignInReturns(SignInResult.Success);
        f.UserHasRole(AppRoles.Applicant, false);
        var model = f.StaffLogin();

        var result = await model.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Dashboard", redirect.PageName);
        var log = Assert.Single(await f.AuditLogsAsync());
        Assert.True(log.IsActive);
        Assert.Equal(f.User.Id, log.CreatedById);
    }

    [Fact]
    public async Task StaffLogin_HonoursALocalReturnUrl()
    {
        var f = new Fixture();
        f.SignInReturns(SignInResult.Success);
        f.UserHasRole(AppRoles.Applicant, false);
        var model = f.StaffLogin();

        var result = await model.OnPostAsync("/Questions");

        Assert.Equal("/Questions", Assert.IsType<LocalRedirectResult>(result).Url);
    }

    [Theory]
    [InlineData("https://evil.example/phish")]
    [InlineData("//evil.example")]
    public async Task StaffLogin_IgnoresAnExternalReturnUrl(string returnUrl)
    {
        var f = new Fixture();
        f.SignInReturns(SignInResult.Success);
        f.UserHasRole(AppRoles.Applicant, false);
        var model = f.StaffLogin();

        var result = await model.OnPostAsync(returnUrl);

        Assert.Equal("/Dashboard", Assert.IsType<RedirectToPageResult>(result).PageName);
    }

    [Fact]
    public async Task StaffLogin_RejectsAnApplicantAndSignsThemBackOut()
    {
        var f = new Fixture();
        f.SignInReturns(SignInResult.Success);
        f.UserHasRole(AppRoles.Applicant);
        var model = f.StaffLogin();

        var result = await model.OnPostAsync();

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
        f.SignIn.Verify(s => s.SignOutAsync(), Times.Once);
        Assert.Contains("Applicants must sign in from the Applicant Login page.",
            model.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage));
        Assert.False(Assert.Single(await f.AuditLogsAsync()).IsActive);
    }

    [Fact]
    public async Task ApplicantLogin_RejectsAStaffAccountWithoutCheckingItsPassword()
    {
        var f = new Fixture();
        f.UserHasRole(AppRoles.Applicant, false);
        var model = f.ApplicantLogin();

        var result = await model.OnPostAsync();

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
        f.SignIn.Verify(s => s.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        Assert.Contains("Invalid username/email or password.", model.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public async Task ApplicantLogin_Succeeds_RedirectsToThePortal()
    {
        var f = new Fixture();
        f.UserHasRole(AppRoles.Applicant);
        f.SignInReturns(SignInResult.Success);
        var model = f.ApplicantLogin();

        var result = await model.OnPostAsync();

        Assert.Equal("/ApplicantPortal", Assert.IsType<RedirectToPageResult>(result).PageName);
        Assert.True(Assert.Single(await f.AuditLogsAsync()).IsActive);
    }

    [Fact]
    public async Task ApplicantLogin_WithTheWrongPassword_Fails()
    {
        var f = new Fixture();
        f.UserHasRole(AppRoles.Applicant);
        f.SignInReturns(SignInResult.Failed);
        var model = f.ApplicantLogin();

        var result = await model.OnPostAsync();

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
        Assert.False(Assert.Single(await f.AuditLogsAsync()).IsActive);
    }

    [Fact]
    public async Task ApplicantLogin_WhenLockedOut_ShowsTheLockoutMessage()
    {
        var f = new Fixture();
        f.UserHasRole(AppRoles.Applicant);
        f.SignInReturns(SignInResult.LockedOut);
        var model = f.ApplicantLogin();

        await model.OnPostAsync();

        Assert.Contains("This account has been locked out. Please try again later.",
            model.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage));
    }
}
