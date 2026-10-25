using System.Security.Claims;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AAExamManagementSystem.Tests;

internal static class TestSupport
{
    public static ApplicationDbContext CreateContext(string? databaseName = null) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options);

    public static Mock<UserManager<ApplicationUser>> CreateUserManager() =>
        new(Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);

    public static Mock<SignInManager<ApplicationUser>> CreateSignInManager(UserManager<ApplicationUser> userManager) =>
        new(
            userManager,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null!, null!, null!, null!);

    public static ClaimsPrincipal PrincipalFor(params string[] roles)
    {
        var claims = roles.Select(role => new Claim(ClaimTypes.Role, role)).ToList();
        claims.Add(new Claim(ClaimTypes.NameIdentifier, "test-user"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    // Gives a page model a signed-in user, TempData and a URL helper so handlers can run outside the pipeline.
    public static void Attach(PageModel model, ClaimsPrincipal user)
    {
        var httpContext = new DefaultHttpContext { User = user };
        model.PageContext = new PageContext { HttpContext = httpContext };
        model.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.IsLocalUrl(It.IsAny<string?>()))
            .Returns((string? target) => !string.IsNullOrEmpty(target) && target.StartsWith('/') && !target.StartsWith("//"));
        model.Url = url.Object;
    }

    public static Mock<UserManager<ApplicationUser>> UserManagerReturning(ApplicationUser? currentUser)
    {
        var userManager = CreateUserManager();
        userManager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(currentUser);
        return userManager;
    }
}
