using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Moq;

namespace AAExamManagementSystem.Tests;

public class ActiveIdentityValidatorTests
{
    private sealed class Fixture
    {
        public ApplicationDbContext Db { get; } = TestSupport.CreateContext();
        public ActiveIdentityValidator Validator { get; }

        public Fixture()
        {
            Validator = new ActiveIdentityValidator(Db);
        }

        public ApplicationUser AddUser(string id, string userName, string email, bool archived = false)
        {
            var user = new ApplicationUser
            {
                Id = id,
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                IsDeleted = archived,
            };
            Db.Users.Add(user);
            Db.SaveChanges();
            return user;
        }

        public Applicant AddApplicant(string userId, string studentNo, string email, bool archived = false)
        {
            var applicant = new Applicant
            {
                UserId = userId,
                StudentNo = studentNo,
                Email = email,
                FirstName = "A",
                LastName = "B",
                AcademicYear = "2026-2027",
                IsActive = !archived,
            };
            Db.Applicants.Add(applicant);
            Db.SaveChanges();
            return applicant;
        }
    }

    [Theory]
    [InlineData("staff", false)]
    [InlineData("  STAFF ", false)]
    [InlineData("someone-else", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public async Task UserName_IsTakenOnlyByAnActiveUser(string? userName, bool expectedAvailable)
    {
        var f = new Fixture();
        f.AddUser("u1", "staff", "staff@example.test");

        Assert.Equal(expectedAvailable, await f.Validator.IsUserNameAvailableAsync(userName));
    }

    [Fact]
    public async Task UserName_OfAnArchivedUserCanBeReused()
    {
        var f = new Fixture();
        f.AddUser("u1", "staff", "staff@example.test", archived: true);

        Assert.True(await f.Validator.IsUserNameAvailableAsync("staff"));
    }

    [Fact]
    public async Task UserName_DoesNotBlockTheUserBeingEdited()
    {
        var f = new Fixture();
        f.AddUser("u1", "staff", "staff@example.test");

        Assert.True(await f.Validator.IsUserNameAvailableAsync("staff", excludeUserId: "u1"));
        Assert.False(await f.Validator.IsUserNameAvailableAsync("staff", excludeUserId: "u2"));
    }

    [Fact]
    public async Task Email_IsTakenByAnActiveUserIgnoringCaseAndSpaces()
    {
        var f = new Fixture();
        f.AddUser("u1", "staff", "Staff@Example.test");

        Assert.False(await f.Validator.IsEmailAvailableAsync(" staff@example.TEST "));
        Assert.True(await f.Validator.IsEmailAvailableAsync("other@example.test"));
    }

    [Fact]
    public async Task Email_OfAnArchivedUserOrApplicantCanBeReused()
    {
        var f = new Fixture();
        f.AddUser("u1", "gone", "gone@example.test", archived: true);
        f.AddApplicant("u2", "2026-1", "old@example.test", archived: true);

        Assert.True(await f.Validator.IsEmailAvailableAsync("gone@example.test"));
        Assert.True(await f.Validator.IsEmailAvailableAsync("old@example.test"));
    }

    [Fact]
    public async Task Email_IsTakenByAnActiveApplicantEvenWhenTheAccountEmailDiffers()
    {
        var f = new Fixture();
        f.AddUser("u1", "ana", "account@example.test");
        f.AddApplicant("u1", "2026-1", "contact@example.test");

        Assert.False(await f.Validator.IsEmailAvailableAsync("contact@example.test"));
    }

    [Fact]
    public async Task Email_DoesNotBlockTheUserOrApplicantBeingEdited()
    {
        var f = new Fixture();
        f.AddUser("u1", "ana", "ana@example.test");
        var applicant = f.AddApplicant("u1", "2026-1", "contact@example.test");

        Assert.True(await f.Validator.IsEmailAvailableAsync("ana@example.test", excludeUserId: "u1"));
        Assert.True(await f.Validator.IsEmailAvailableAsync("contact@example.test", excludeUserId: "u1", excludeApplicantId: applicant.Id));
        Assert.False(await f.Validator.IsEmailAvailableAsync("contact@example.test", excludeUserId: "u2"));
    }

    [Theory]
    [InlineData("2026-00042", false)]
    [InlineData("  2026-00042 ", false)]
    [InlineData("2026-00043", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    public async Task StudentNo_IsTakenOnlyByAnActiveApplicant(string? studentNo, bool expectedAvailable)
    {
        var f = new Fixture();
        f.AddApplicant("u1", "2026-00042", "ana@example.test");

        Assert.Equal(expectedAvailable, await f.Validator.IsStudentNoAvailableAsync(studentNo));
    }

    [Fact]
    public async Task StudentNo_OfAnArchivedApplicantCanBeReused()
    {
        var f = new Fixture();
        f.AddApplicant("u1", "2026-00042", "ana@example.test", archived: true);

        Assert.True(await f.Validator.IsStudentNoAvailableAsync("2026-00042"));
    }

    [Fact]
    public async Task StudentNo_DoesNotBlockTheApplicantBeingEdited()
    {
        var f = new Fixture();
        var applicant = f.AddApplicant("u1", "2026-00042", "ana@example.test");

        Assert.True(await f.Validator.IsStudentNoAvailableAsync("2026-00042", excludeApplicantId: applicant.Id));
    }

    [Fact]
    public async Task Validate_AddsAnErrorUnderTheKeyOnlyWhenTheValueIsTaken()
    {
        var f = new Fixture();
        f.AddUser("u1", "staff", "staff@example.test");
        f.AddApplicant("u2", "2026-00042", "ana@example.test");
        var state = new ModelStateDictionary();

        Assert.False(await f.Validator.ValidateUserNameAsync(state, "UserName", "Staff"));
        Assert.False(await f.Validator.ValidateEmailAsync(state, "Email", "staff@example.test"));
        Assert.False(await f.Validator.ValidateStudentNoAsync(state, "StudentNo", "2026-00042"));
        Assert.True(await f.Validator.ValidateUserNameAsync(state, "Other", "free"));

        Assert.Equal(ActiveIdentityValidator.UserNameMessage("Staff"), state["UserName"]!.Errors.Single().ErrorMessage);
        Assert.Equal(ActiveIdentityValidator.EmailMessage("staff@example.test"), state["Email"]!.Errors.Single().ErrorMessage);
        Assert.Equal(ActiveIdentityValidator.StudentNoMessage("2026-00042"), state["StudentNo"]!.Errors.Single().ErrorMessage);
        Assert.False(state.ContainsKey("Other"));
    }

    [Fact]
    public async Task UserStore_SkipsArchivedUsersWhenLookingUpByNameOrEmail()
    {
        var f = new Fixture();
        f.AddUser("old", "staff", "staff@example.test", archived: true);
        var current = f.AddUser("new", "staff", "staff@example.test");
        var store = new ActiveUserStore(f.Db);

        Assert.Equal(current.Id, (await store.FindByNameAsync("STAFF"))!.Id);
        Assert.Equal(current.Id, (await store.FindByEmailAsync("STAFF@EXAMPLE.TEST"))!.Id);
        Assert.Equal("old", (await store.FindByIdAsync("old"))!.Id);
    }

    [Fact]
    public async Task UserStore_FindsNothingWhenOnlyAnArchivedUserHoldsTheName()
    {
        var f = new Fixture();
        f.AddUser("old", "staff", "staff@example.test", archived: true);
        var store = new ActiveUserStore(f.Db);

        Assert.Null(await store.FindByNameAsync("STAFF"));
        Assert.Null(await store.FindByEmailAsync("STAFF@EXAMPLE.TEST"));
    }

    [Fact]
    public async Task ArchivingAnApplicant_ArchivesTheirAccountSoTheUsernameIsFree()
    {
        var f = new Fixture();
        var user = f.AddUser("u1", "ana", "ana@example.test");
        var applicant = f.AddApplicant("u1", "2026-00042", "ana@example.test");
        var userManager = TestSupport.CreateUserManager();
        userManager.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync(user);
        userManager.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.UpdateSecurityStampAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        var page = new AAExamManagementSystem.Pages.Applicants.IndexModel(f.Db, userManager.Object);
        TestSupport.Attach(page, TestSupport.PrincipalFor(Roles.Admin));

        await page.OnPostArchiveAsync(applicant.Id);

        Assert.True(user.IsDeleted);
        Assert.False(f.Db.Applicants.Single().IsActive);
        Assert.True(await f.Validator.IsUserNameAvailableAsync("ana"));
        Assert.True(await f.Validator.IsEmailAvailableAsync("ana@example.test"));
        Assert.True(await f.Validator.IsStudentNoAvailableAsync("2026-00042"));
    }
}
