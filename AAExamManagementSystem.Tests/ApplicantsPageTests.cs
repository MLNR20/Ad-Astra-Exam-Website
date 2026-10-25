using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Pages.Applicants;
using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Mvc;
using AppRoles = AAExamManagementSystem.Models.Entities.Roles;

namespace AAExamManagementSystem.Tests;

public class ApplicantsPageTests
{
    private const int WebDevId = 1;
    private const int PhotoId = 2;

    private sealed class Fixture
    {
        public ApplicationDbContext Db { get; } = TestSupport.CreateContext();
        public Applicant WebDevApplicant { get; }
        public Applicant PhotoApplicant { get; }
        public Applicant BothApplicant { get; }
        public Applicant InactiveApplicant { get; }

        public Fixture()
        {
            Db.Sections.AddRange(
                new Section { Id = WebDevId, Name = "Web Development" },
                new Section { Id = PhotoId, Name = "Photo" });
            Db.QuestionTypes.Add(new QuestionType { Id = 1, Name = QuestionTypes.MultipleChoice });

            WebDevApplicant = AddApplicant("Ana", "Cruz", daysAgo: 3, "Web Development");
            PhotoApplicant = AddApplicant("Ben", "Diaz", daysAgo: 2, "Photo");
            BothApplicant = AddApplicant("Cai", "Eve", daysAgo: 1, "Web Development", "Photo");
            InactiveApplicant = AddApplicant("Dee", "Fox", daysAgo: 0, "Web Development");
            InactiveApplicant.IsActive = false;

            Db.SaveChanges();
        }

        private Applicant AddApplicant(string first, string last, int daysAgo, params string[] sections)
        {
            var applicant = new Applicant
            {
                FirstName = first,
                LastName = last,
                StudentNo = $"2026-{first}",
                Email = $"{first.ToLower()}@example.test",
                AcademicYear = "2026-2027",
                UserId = $"user-{first}",
                DateCreated = DateTime.UtcNow.AddDays(-daysAgo),
            };

            foreach (var section in sections)
            {
                applicant.Attempts.Add(new Attempt { Section = section, Password = $"{first}-{section}" });
            }

            Db.Applicants.Add(applicant);
            return applicant;
        }

        public IndexModel IndexAs(string role, int? sectionId)
        {
            var user = sectionId is null ? null : new ApplicationUser { Id = "editor", SectionId = sectionId };
            var model = new IndexModel(Db, TestSupport.UserManagerReturning(user).Object);
            TestSupport.Attach(model, TestSupport.PrincipalFor(role));
            return model;
        }

        public DetailsModel DetailsAs(string role, int? sectionId)
        {
            var user = sectionId is null ? null : new ApplicationUser { Id = "editor", SectionId = sectionId };
            var model = new DetailsModel(Db, TestSupport.UserManagerReturning(user).Object);
            TestSupport.Attach(model, TestSupport.PrincipalFor(role));
            return model;
        }
    }

    [Fact]
    public async Task Index_ForAnAdmin_ListsEveryActiveApplicantNewestFirst()
    {
        var f = new Fixture();
        var model = f.IndexAs(AppRoles.Admin, null);

        await model.OnGetAsync();

        Assert.True(model.IsAdmin);
        Assert.Equal(new[] { "Cai Eve", "Ben Diaz", "Ana Cruz" }, model.Applicants.Select(a => a.FullName));
    }

    [Fact]
    public async Task Index_ForAnEditor_ListsOnlyApplicantsWhoAppliedToTheirDepartment()
    {
        var f = new Fixture();
        var model = f.IndexAs(AppRoles.Editor, WebDevId);

        await model.OnGetAsync();

        Assert.False(model.IsAdmin);
        Assert.Equal("Web Development", model.DepartmentName);
        Assert.Equal(new[] { "Cai Eve", "Ana Cruz" }, model.Applicants.Select(a => a.FullName));
    }

    [Fact]
    public async Task Index_ForAnEditor_NeverIncludesInactiveApplicants()
    {
        var f = new Fixture();
        var model = f.IndexAs(AppRoles.Editor, WebDevId);

        await model.OnGetAsync();

        Assert.DoesNotContain(model.Applicants, a => a.Id == f.InactiveApplicant.Id);
    }

    [Fact]
    public async Task Index_ForAnEditorWithoutADepartment_ListsNobody()
    {
        var f = new Fixture();
        var model = f.IndexAs(AppRoles.Editor, null);

        await model.OnGetAsync();

        Assert.Empty(model.Applicants);
        Assert.Null(model.DepartmentName);
    }

    [Fact]
    public async Task Index_ForAnEditorWhoseDepartmentNoLongerExists_ListsNobody()
    {
        var f = new Fixture();
        var model = f.IndexAs(AppRoles.Editor, 99);

        await model.OnGetAsync();

        Assert.Empty(model.Applicants);
    }

    [Fact]
    public async Task Index_ListsEachExamTheApplicantAppliedFor()
    {
        var f = new Fixture();
        var model = f.IndexAs(AppRoles.Admin, null);

        await model.OnGetAsync();

        var both = model.Applicants.Single(a => a.Id == f.BothApplicant.Id);
        Assert.Equal(new[] { "Photo", "Web Development" }, both.AppliedSections.Order());
    }

    [Fact]
    public async Task Details_ForAnEditor_ShowsTheApplicantWithOnlyTheirDepartmentsExam()
    {
        var f = new Fixture();
        var model = f.DetailsAs(AppRoles.Editor, PhotoId);

        var result = await model.OnGetAsync(f.BothApplicant.Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
        Assert.Equal("Cai", model.Applicant.FirstName);
        Assert.Equal("Photo", Assert.Single(model.Attempts).Section);
    }

    [Fact]
    public async Task Details_ForAnEditor_HidesApplicantsFromOtherDepartments()
    {
        var f = new Fixture();
        var model = f.DetailsAs(AppRoles.Editor, PhotoId);

        var result = await model.OnGetAsync(f.WebDevApplicant.Id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ForAnEditorWithoutADepartment_ReturnsNotFound()
    {
        var f = new Fixture();
        var model = f.DetailsAs(AppRoles.Editor, null);

        var result = await model.OnGetAsync(f.WebDevApplicant.Id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Details_ForAnAdmin_ShowsEveryExam()
    {
        var f = new Fixture();
        var model = f.DetailsAs(AppRoles.Admin, null);

        await model.OnGetAsync(f.BothApplicant.Id);

        Assert.Equal(new[] { "Photo", "Web Development" }, model.Attempts.Select(a => a.Section));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9999)]
    public async Task Details_WithAnUnknownId_ReturnsNotFound(int id)
    {
        var f = new Fixture();
        var model = f.DetailsAs(AppRoles.Admin, null);

        Assert.IsType<NotFoundResult>(await model.OnGetAsync(id));
    }

    [Fact]
    public async Task Details_WithAnInactiveApplicant_ReturnsNotFound()
    {
        var f = new Fixture();
        var model = f.DetailsAs(AppRoles.Admin, null);

        Assert.IsType<NotFoundResult>(await model.OnGetAsync(f.InactiveApplicant.Id));
    }

    [Fact]
    public async Task Details_ReportsScoreAndEvaluationStatusOfASubmittedExam()
    {
        var f = new Fixture();
        var attempt = f.Db.Attempts.Single(a => a.ApplicantId == f.WebDevApplicant.Id);
        var graded = new Question { QuestionTitle = "Graded", QuestionTypeId = 1, SectionId = WebDevId, Score = 4 };
        var essay = new Question { QuestionTitle = "Essay", QuestionTypeId = 1, SectionId = WebDevId, Score = 6 };
        f.Db.Questions.AddRange(graded, essay);
        attempt.DateTaken = DateTime.UtcNow.ToString("o");
        attempt.TotalScore = 4;
        attempt.Answers.Add(new Answer { ApplicantId = attempt.ApplicantId, QuestionId = graded.Id, IsCorrect = "Yes", Point = 4 });
        attempt.Answers.Add(new Answer { ApplicantId = attempt.ApplicantId, QuestionId = essay.Id, IsCorrect = null });
        await f.Db.SaveChangesAsync();

        var model = f.DetailsAs(AppRoles.Admin, null);
        await model.OnGetAsync(f.WebDevApplicant.Id);

        var item = Assert.Single(model.Attempts);
        Assert.True(item.IsSubmitted);
        Assert.Equal(4, item.TotalScore);
        Assert.Equal(10, item.MaxScore);
        Assert.Equal(1, item.PendingEvaluationCount);
        Assert.False(item.IsApproved);
    }

    [Fact]
    public async Task Details_MarksAnExamThatHasNotBeenTakenAsNotSubmitted()
    {
        var f = new Fixture();
        var model = f.DetailsAs(AppRoles.Admin, null);

        await model.OnGetAsync(f.PhotoApplicant.Id);

        Assert.False(Assert.Single(model.Attempts).IsSubmitted);
    }
}
