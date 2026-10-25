using System.Reflection;
using AAExamManagementSystem.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using AppRoles = AAExamManagementSystem.Models.Entities.Roles;

namespace AAExamManagementSystem.Tests;

public class AuthorizationTests
{
    private static readonly Assembly WebAssembly = typeof(NotificationHub).Assembly;

    private static readonly string[] AdminOnlyNamespaces =
    {
        "Users", "Roles", "RoleAssignments", "AuditLogs", "Departments", "Courses", "Sections", "Announcements", "Choices",
    };

    private static readonly string[] ApplicantPageTypes =
    {
        "AAExamManagementSystem.Pages.ApplicantPortal",
        "AAExamManagementSystem.Pages.Attempts.Take",
        "AAExamManagementSystem.Pages.Attempts.MyAttempts",
        "AAExamManagementSystem.Pages.Attempts.MyAttemptDetails",
    };

    private static IEnumerable<Type> PageModels() =>
        WebAssembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && typeof(PageModel).IsAssignableFrom(t));

    private static string[] RolesOf(Type type)
    {
        var authorize = type.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        return (authorize!.Roles ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Order()
            .ToArray();
    }

    private static Type PageModelOf(string pageTypeName) =>
        WebAssembly.GetType(pageTypeName + "Model", throwOnError: true)!;

    public static IEnumerable<object[]> AdminOnlyPageModels() =>
        PageModels()
            .Where(t => AdminOnlyNamespaces.Any(ns => t.Namespace == $"AAExamManagementSystem.Pages.{ns}"))
            .Select(t => new object[] { t.FullName! });

    public static IEnumerable<object[]> StaffPageModels() =>
        new[]
        {
            "AAExamManagementSystem.Pages.Applicants.Index",
            "AAExamManagementSystem.Pages.Applicants.Details",
            "AAExamManagementSystem.Pages.Attempts.Index",
            "AAExamManagementSystem.Pages.Attempts.Review",
            "AAExamManagementSystem.Pages.PageSections.Index",
            "AAExamManagementSystem.Pages.Questions.Index",
            "AAExamManagementSystem.Pages.Questions.Edit",
            "AAExamManagementSystem.Pages.Questions.Details",
            "AAExamManagementSystem.Pages.Questions.Delete",
            "AAExamManagementSystem.Pages.Questions.Simulate",
            "AAExamManagementSystem.Pages.Questions.SimulateAll",
            "AAExamManagementSystem.Pages.Analytics.Index",
        }.Select(name => new object[] { name });

    [Fact]
    public void AdminOnlyAreas_AreFound()
    {
        Assert.NotEmpty(AdminOnlyPageModels());
    }

    [Theory]
    [MemberData(nameof(AdminOnlyPageModels))]
    public void AdministrationPages_AreRestrictedToAdmins(string typeName)
    {
        var type = WebAssembly.GetType(typeName, throwOnError: true)!;

        Assert.Equal(new[] { AppRoles.Admin }, RolesOf(type));
    }

    [Theory]
    [MemberData(nameof(StaffPageModels))]
    public void ReviewAndAuthoringPages_AreForStaffAndNeverForApplicants(string pageTypeName)
    {
        var roles = RolesOf(PageModelOf(pageTypeName));

        Assert.Contains(AppRoles.Admin, roles);
        Assert.Contains(AppRoles.Editor, roles);
        Assert.DoesNotContain(AppRoles.Applicant, roles);
        Assert.DoesNotContain(AppRoles.Student, roles);
        Assert.DoesNotContain(AppRoles.Guest, roles);
    }

    [Theory]
    [InlineData("AAExamManagementSystem.Pages.Applicants.Index")]
    [InlineData("AAExamManagementSystem.Pages.Applicants.Details")]
    [InlineData("AAExamManagementSystem.Pages.Questions.Simulate")]
    [InlineData("AAExamManagementSystem.Pages.Questions.SimulateAll")]
    public void ApplicantRecordsAndPreviews_AreLimitedToAdminsAndEditors(string pageTypeName)
    {
        Assert.Equal(new[] { AppRoles.Admin, AppRoles.Editor }, RolesOf(PageModelOf(pageTypeName)));
    }

    [Theory]
    [MemberData(nameof(ApplicantPages))]
    public void ExamTakingPages_AreRestrictedToApplicants(string pageTypeName)
    {
        Assert.Equal(new[] { AppRoles.Applicant }, RolesOf(PageModelOf(pageTypeName)));
    }

    public static IEnumerable<object[]> ApplicantPages() => ApplicantPageTypes.Select(name => new object[] { name });

    [Fact]
    public void DepartmentStaffPage_IsRestrictedToEditors()
    {
        Assert.Equal(new[] { AppRoles.Editor }, RolesOf(PageModelOf("AAExamManagementSystem.Pages.Staff.Index")));
    }

    [Fact]
    public void EveryApiController_RequiresAnAuthenticatedUser()
    {
        var controllers = WebAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .ToList();

        Assert.NotEmpty(controllers);
        foreach (var controller in controllers)
        {
            Assert.True(
                controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any(),
                $"{controller.Name} is missing [Authorize].");
            Assert.False(
                controller.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any(),
                $"{controller.Name} must not allow anonymous access.");
        }
    }

    [Fact]
    public void TheNotificationHub_RequiresAnAuthenticatedUser()
    {
        Assert.True(typeof(Hub).IsAssignableFrom(typeof(NotificationHub)));
        Assert.True(typeof(NotificationHub).GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any());
    }

    [Fact]
    public void PagesWithoutAnAttribute_AreOnlyTheOnesThatRelyOnTheFolderConvention()
    {
        // Program.cs authorizes the whole Pages folder and opens a short list of pages to everyone, so a page
        // model without [Authorize] is either on that list or only needs a signed-in user. Any new page that
        // shows up here should be a deliberate choice.
        var withoutAttribute = PageModels()
            .Where(t => t.GetCustomAttribute<AuthorizeAttribute>() is null)
            .Select(t => t.Namespace!.Replace("AAExamManagementSystem.Pages", string.Empty).TrimStart('.') + "." + t.Name)
            .Select(name => name.TrimStart('.'))
            .Order()
            .ToArray();

        var expected = new[]
        {
            "ApplicantLoginModel",
            "ErrorModel",
            "ForgotPasswordConfirmationModel",
            "ForgotPasswordModel",
            "IndexModel",
            "LoginModel",
            "LogoutModel",
            "PrivacyModel",
            "ResetPasswordModel",
            "SignupModel",
        };

        Assert.Equal(expected, withoutAttribute);
    }

    [Fact]
    public void RoleNames_AreUniqueAndListedInAll()
    {
        var constants = typeof(AppRoles)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.Equal(constants.Count, constants.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(constants.Order(), AppRoles.All.Order());
    }
}
