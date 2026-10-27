using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Pages;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AAExamManagementSystem.Tests;

public class SignupStudentNoTests
{
    private sealed class Fixture
    {
        public ApplicationDbContext Db { get; } = TestSupport.CreateContext();

        public Fixture()
        {
            Db.Applicants.Add(new Applicant
            {
                StudentNo = "2026-00042",
                FirstName = "Ana",
                LastName = "Cruz",
                Email = "ana@example.test",
                AcademicYear = "2026-2027",
                UserId = "existing-user",
            });
            Db.SaveChanges();
        }

        public SignupModel Page(string studentNo)
        {
            var userManager = TestSupport.CreateUserManager();
            var model = new SignupModel(
                userManager.Object,
                TestSupport.CreateSignInManager(userManager.Object).Object,
                Mock.Of<IGenericRepository<Applicant>>(),
                Mock.Of<IMapper>(),
                Db,
                Mock.Of<IRegistrationNotifier>())
            {
                Input = new ApplicantRegisterDto { StudentNo = studentNo },
            };
            TestSupport.Attach(model, TestSupport.PrincipalFor());
            return model;
        }
    }

    [Theory]
    [InlineData("2026-00042")]
    [InlineData("  2026-00042  ")]
    public async Task Post_WithARegisteredStudentNo_IsRejected(string studentNo)
    {
        var page = new Fixture().Page(studentNo);

        var result = await page.OnPostAsync();

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
        Assert.Equal("That student number is already registered.",
            page.ModelState["Input.StudentNo"]!.Errors.Single().ErrorMessage);
    }

    [Fact]
    public async Task Post_WithADifferentStudentNo_IsNotFlagged()
    {
        var page = new Fixture().Page("2026-99999");
        page.ModelState.AddModelError(string.Empty, "stop after student number validation");

        await page.OnPostAsync();

        Assert.False(page.ModelState.ContainsKey("Input.StudentNo"));
    }

    [Fact]
    public async Task Post_StoresTheStudentNoWithoutSurroundingSpaces()
    {
        var page = new Fixture().Page("  2026-99999 ");
        page.ModelState.AddModelError(string.Empty, "stop after student number validation");

        await page.OnPostAsync();

        Assert.Equal("2026-99999", page.Input.StudentNo);
    }

    [Fact]
    public async Task Post_ComparesStudentNumbersIgnoringCase()
    {
        var fixture = new Fixture();
        fixture.Db.Applicants.Add(new Applicant
        {
            StudentNo = "ab-100", FirstName = "B", LastName = "C", Email = "b@example.test",
            AcademicYear = "2026-2027", UserId = "other-user",
        });
        fixture.Db.SaveChanges();
        var page = fixture.Page("AB-100");

        await page.OnPostAsync();

        Assert.True(page.ModelState.ContainsKey("Input.StudentNo"));
    }

    [Fact]
    public async Task Post_CountsInactiveApplicantsAsTaken()
    {
        var fixture = new Fixture();
        fixture.Db.Applicants.Single().IsActive = false;
        fixture.Db.SaveChanges();
        var page = fixture.Page("2026-00042");

        await page.OnPostAsync();

        Assert.True(page.ModelState.ContainsKey("Input.StudentNo"));
    }

    [Theory]
    [InlineData("2026-00042", false)]
    [InlineData(" 2026-00042", false)]
    [InlineData("2026-00043", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public async Task CheckStudentNo_ReportsWhetherTheNumberIsFree(string? studentNo, bool expectedAvailable)
    {
        var page = new Fixture().Page("unused");

        var result = Assert.IsType<JsonResult>(await page.OnPostCheckStudentNoAsync(studentNo));

        var available = (bool)result.Value!.GetType().GetProperty("available")!.GetValue(result.Value)!;
        Assert.Equal(expectedAvailable, available);
    }
}

public class SignupSchoolTests
{
    private static SignupModel Page(string? school)
    {
        var userManager = TestSupport.CreateUserManager();
        var model = new SignupModel(
            userManager.Object,
            TestSupport.CreateSignInManager(userManager.Object).Object,
            Mock.Of<IGenericRepository<Applicant>>(),
            Mock.Of<IMapper>(),
            TestSupport.CreateContext(),
            Mock.Of<IRegistrationNotifier>())
        {
            Input = new ApplicantRegisterDto { School = school },
        };
        TestSupport.Attach(model, TestSupport.PrincipalFor());
        // Stops the post right after the field checks.
        model.ModelState.AddModelError(string.Empty, "stop after validation");
        return model;
    }

    [Fact]
    public async Task Post_WithASchoolFromTheList_IsAccepted()
    {
        var page = Page(BenildeSchools.All[0]);

        await page.OnPostAsync();

        Assert.False(page.ModelState.ContainsKey("Input.School"));
        Assert.Equal(BenildeSchools.All[0], page.Input.School);
    }

    [Fact]
    public async Task Post_UsesTheListedSpellingOfTheSchool()
    {
        var page = Page("  " + BenildeSchools.All[1].ToUpperInvariant() + "  ");

        await page.OnPostAsync();

        Assert.Equal(BenildeSchools.All[1], page.Input.School);
    }

    [Fact]
    public async Task Post_WithASchoolThatIsNotInTheList_IsRejected()
    {
        var page = Page("Hogwarts");

        await page.OnPostAsync();

        Assert.Equal("Please pick a school from the list.", page.ModelState["Input.School"]!.Errors.Single().ErrorMessage);
        Assert.Null(page.Input.School);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Post_WithNoSchool_LeavesItOptional(string? school)
    {
        var page = Page(school);

        await page.OnPostAsync();

        Assert.False(page.ModelState.ContainsKey("Input.School"));
        Assert.Null(page.Input.School);
    }

    [Fact]
    public void TheListHasNoDuplicates()
    {
        Assert.Equal(BenildeSchools.All.Length, BenildeSchools.All.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}

public class SignupSuccessTests
{
    private static SignupSuccessModel Page(string? name = null, string? email = null)
    {
        var model = new SignupSuccessModel();
        TestSupport.Attach(model, TestSupport.PrincipalFor("Applicant"));
        if (name is not null)
        {
            model.TempData[SignupSuccessModel.SubmittedNameKey] = name;
            model.TempData[SignupSuccessModel.SubmittedEmailKey] = email;
        }

        return model;
    }

    [Fact]
    public void RightAfterRegistering_ShowsTheApplicantsNameAndEmail()
    {
        var model = Page("Ana Cruz", "ana@example.test");

        var result = model.OnGet();

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
        Assert.Equal("Ana Cruz", model.ApplicantName);
        Assert.Equal("ana@example.test", model.Email);
    }

    [Fact]
    public void OpenedDirectly_SendsTheApplicantToTheirPortal()
    {
        var result = Page().OnGet();

        Assert.Equal("/ApplicantPortal", Assert.IsType<RedirectToPageResult>(result).PageName);
    }
}
