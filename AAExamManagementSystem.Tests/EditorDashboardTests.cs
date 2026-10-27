using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Pages;
using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Mvc;
using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using ReviewModel = AAExamManagementSystem.Pages.Attempts.ReviewModel;
using AttemptsIndexModel = AAExamManagementSystem.Pages.Attempts.IndexModel;

namespace AAExamManagementSystem.Tests;

public class EditorDashboardTests
{
    private const int WebDevId = 1;
    private const int PhotoId = 2;

    private sealed class Fixture
    {
        public ApplicationDbContext Db { get; } = TestSupport.CreateContext();
        public Applicant WebDevApplicant { get; }
        public Applicant PhotoApplicant { get; }
        public Applicant BothApplicant { get; }

        public Fixture()
        {
            Db.Sections.AddRange(
                new Section { Id = WebDevId, Name = "Web Development" },
                new Section { Id = PhotoId, Name = "Photo" });

            WebDevApplicant = AddApplicant("Ana", "Cruz", daysAgo: 3, "Web Development");
            PhotoApplicant = AddApplicant("Ben", "Diaz", daysAgo: 2, "Photo");
            BothApplicant = AddApplicant("Cai", "Eve", daysAgo: 1, "Web Development", "Photo");

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

        public DashboardModel DashboardAs(string role, int? sectionId)
        {
            var user = sectionId is null ? null : new ApplicationUser { Id = "editor", SectionId = sectionId };
            var model = new DashboardModel(Db, TestSupport.UserManagerReturning(user).Object);
            TestSupport.Attach(model, TestSupport.PrincipalFor(role));
            return model;
        }

        public AttemptsIndexModel AttemptsAs(string role, int? sectionId)
        {
            var user = sectionId is null ? null : new ApplicationUser { Id = "editor", SectionId = sectionId };
            var model = new AttemptsIndexModel(Db, TestSupport.UserManagerReturning(user).Object);
            TestSupport.Attach(model, TestSupport.PrincipalFor(role));
            return model;
        }

        public ReviewModel ReviewAs(string role, int? sectionId)
        {
            var user = sectionId is null ? null : new ApplicationUser { Id = "editor", SectionId = sectionId, FirstName = "Ed", LastName = "Itor" };
            var model = new ReviewModel(Db, TestSupport.UserManagerReturning(user).Object);
            TestSupport.Attach(model, TestSupport.PrincipalFor(role));
            return model;
        }

        public void Submit(Applicant applicant, string section)
        {
            var attempt = Db.Attempts.Single(a => a.ApplicantId == applicant.Id && a.Section == section);
            attempt.DateTaken = DateTime.UtcNow.ToString("o");
            Db.SaveChanges();
        }
    }

    [Fact]
    public async Task Dashboard_ForAnEditor_ListsOnlyTheirDepartmentsApplicantsNewestFirst()
    {
        var f = new Fixture();
        var model = f.DashboardAs(AppRoles.Editor, WebDevId);

        await model.OnGetAsync();

        Assert.True(model.IsEditor);
        Assert.Equal("Web Development", model.DepartmentName);
        Assert.Equal(new[] { "Cai Eve", "Ana Cruz" }, model.Applicants.Select(a => a.FullName));
    }

    [Fact]
    public async Task Dashboard_ForAnEditor_ReportsWhetherTheDepartmentsExamWasTaken()
    {
        var f = new Fixture();
        f.Submit(f.BothApplicant, "Web Development");
        var model = f.DashboardAs(AppRoles.Editor, WebDevId);

        await model.OnGetAsync();

        Assert.True(model.Applicants.Single(a => a.Id == f.BothApplicant.Id).IsSubmitted);
        Assert.False(model.Applicants.Single(a => a.Id == f.WebDevApplicant.Id).IsSubmitted);
    }

    [Fact]
    public async Task Dashboard_ForAnEditorWithoutADepartment_ListsNobody()
    {
        var f = new Fixture();
        var model = f.DashboardAs(AppRoles.Editor, null);

        await model.OnGetAsync();

        Assert.Empty(model.Applicants);
        Assert.Null(model.DepartmentName);
    }

    [Fact]
    public async Task Dashboard_ForAnAdmin_DoesNotLoadTheApplicantView()
    {
        var f = new Fixture();
        var model = f.DashboardAs(AppRoles.Admin, null);

        await model.OnGetAsync();

        Assert.False(model.IsEditor);
        Assert.Empty(model.Applicants);
    }

    [Fact]
    public async Task Attempts_FilteredByApplicant_ShowsOnlyThatApplicantsSubmittedAttempts()
    {
        var f = new Fixture();
        f.Submit(f.WebDevApplicant, "Web Development");
        f.Submit(f.BothApplicant, "Web Development");
        var model = f.AttemptsAs(AppRoles.Editor, WebDevId);
        model.ApplicantId = f.BothApplicant.Id;

        await model.OnGetAsync();

        Assert.Equal("Cai Eve", Assert.Single(model.Attempts).ApplicantName);
        Assert.Equal("Cai Eve", model.ApplicantName);
    }

    [Fact]
    public async Task Attempts_FilteredByAnApplicantOfAnotherDepartment_ShowsNothingAndNoName()
    {
        var f = new Fixture();
        f.Submit(f.PhotoApplicant, "Photo");
        var model = f.AttemptsAs(AppRoles.Editor, WebDevId);
        model.ApplicantId = f.PhotoApplicant.Id;

        await model.OnGetAsync();

        Assert.Empty(model.Attempts);
        Assert.Null(model.ApplicantName);
    }

    [Fact]
    public async Task Review_ForAnAdmin_IsReadOnlyAndCannotApprove()
    {
        var f = new Fixture();
        f.Submit(f.WebDevApplicant, "Web Development");
        var attemptId = f.Db.Attempts.Single(a => a.ApplicantId == f.WebDevApplicant.Id).Id;
        var model = f.ReviewAs(AppRoles.Admin, null);

        var result = await model.OnPostApproveAsync(attemptId);

        Assert.True(model.IsReadOnly);
        Assert.IsType<ForbidResult>(result);
        Assert.False(f.Db.Attempts.Single(a => a.Id == attemptId).IsApproved);
    }

    [Fact]
    public async Task Review_ForAnAdmin_StillShowsTheAttempt()
    {
        var f = new Fixture();
        f.Submit(f.WebDevApplicant, "Web Development");
        var attemptId = f.Db.Attempts.Single(a => a.ApplicantId == f.WebDevApplicant.Id).Id;
        var model = f.ReviewAs(AppRoles.Admin, null);

        var result = await model.OnGetAsync(attemptId);

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
        Assert.Equal("Ana Cruz", model.Attempt!.ApplicantName);
    }

    [Fact]
    public async Task Review_ForAnEditor_CanApproveTheirDepartmentsAttempt()
    {
        var f = new Fixture();
        f.Submit(f.WebDevApplicant, "Web Development");
        var attemptId = f.Db.Attempts.Single(a => a.ApplicantId == f.WebDevApplicant.Id).Id;
        var model = f.ReviewAs(AppRoles.Editor, WebDevId);

        var result = await model.OnPostApproveAsync(attemptId);

        Assert.False(model.IsReadOnly);
        Assert.IsType<RedirectToPageResult>(result);
        Assert.True(f.Db.Attempts.Single(a => a.Id == attemptId).IsApproved);
    }

    // Gives the applicant's attempt in a department one answer worth `max` points scoring `score`.
    private static void Grade(Fixture f, Applicant applicant, string section, int score, int max, bool approved, bool essayPending = false)
    {
        var sectionId = section == "Web Development" ? WebDevId : PhotoId;
        if (!f.Db.QuestionTypes.Any())
        {
            f.Db.QuestionTypes.Add(new QuestionType { Id = 1, Name = QuestionTypes.MultipleChoice });
        }

        var question = new Question { QuestionTitle = $"Q-{applicant.Id}-{section}", QuestionTypeId = 1, SectionId = sectionId, Score = max };
        f.Db.Questions.Add(question);
        f.Db.SaveChanges();

        var attempt = f.Db.Attempts.Single(a => a.ApplicantId == applicant.Id && a.Section == section);
        attempt.DateTaken = DateTime.UtcNow.ToString("o");
        attempt.TotalScore = score;
        attempt.IsApproved = approved;
        attempt.ApprovedBy = approved ? "Noah Reyes" : null;
        attempt.Answers.Add(new Answer
        {
            ApplicantId = applicant.Id,
            QuestionId = question.Id,
            Point = score,
            IsCorrect = essayPending ? null : "Yes",
        });
        f.Db.SaveChanges();
    }

    [Fact]
    public async Task Dashboard_CountsApplicantsPassedAndAttemptsToCheck()
    {
        var f = new Fixture();
        Grade(f, f.WebDevApplicant, "Web Development", score: 8, max: 10, approved: true);
        Grade(f, f.BothApplicant, "Web Development", score: 5, max: 10, approved: false);
        var model = f.DashboardAs(AppRoles.Editor, WebDevId);

        await model.OnGetAsync();

        Assert.Equal(2, model.TotalApplicants);
        Assert.Equal(1, model.PassedApplicants);
        Assert.Equal(1, model.AttemptsToCheck);
    }

    [Fact]
    public async Task Dashboard_AnApprovedAttemptBelowThePassMarkDoesNotCountAsPassed()
    {
        var f = new Fixture();
        Grade(f, f.WebDevApplicant, "Web Development", score: 5, max: 10, approved: true);
        var model = f.DashboardAs(AppRoles.Editor, WebDevId);

        await model.OnGetAsync();

        Assert.Equal(0, model.PassedApplicants);
        Assert.Equal(0, model.AttemptsToCheck);
    }

    [Theory]
    [InlineData(7, true)]
    [InlineData(6, false)]
    public async Task Dashboard_ThePassMarkIsSeventyPercent(int score, bool passes)
    {
        var f = new Fixture();
        Grade(f, f.WebDevApplicant, "Web Development", score: score, max: 10, approved: true);
        var model = f.DashboardAs(AppRoles.Editor, WebDevId);

        await model.OnGetAsync();

        Assert.Equal(70, DashboardModel.PassingPercentage);
        Assert.Equal(passes ? 1 : 0, model.PassedApplicants);
    }

    [Fact]
    public async Task Dashboard_AnUnapprovedAttemptThatScoredWellIsNotYetPassed()
    {
        var f = new Fixture();
        Grade(f, f.WebDevApplicant, "Web Development", score: 10, max: 10, approved: false);
        var model = f.DashboardAs(AppRoles.Editor, WebDevId);

        await model.OnGetAsync();

        Assert.Equal(0, model.PassedApplicants);
        Assert.Equal(1, model.AttemptsToCheck);
    }

    [Fact]
    public async Task Dashboard_AttentionListHoldsOnlyUnapprovedSubmittedAttemptsOfTheirDepartment()
    {
        var f = new Fixture();
        Grade(f, f.WebDevApplicant, "Web Development", score: 4, max: 10, approved: false, essayPending: true);
        Grade(f, f.BothApplicant, "Web Development", score: 9, max: 10, approved: true);
        Grade(f, f.PhotoApplicant, "Photo", score: 1, max: 10, approved: false);
        var model = f.DashboardAs(AppRoles.Editor, WebDevId);

        await model.OnGetAsync();

        var item = Assert.Single(model.Attention);
        Assert.Equal("Ana Cruz", item.ApplicantName);
        Assert.Equal(1, item.PendingEvaluationCount);
    }

    [Fact]
    public async Task Dashboard_RecentActivityListsSubmissionsAndApprovalsNewestFirst()
    {
        var f = new Fixture();
        Grade(f, f.WebDevApplicant, "Web Development", score: 4, max: 10, approved: false);
        Grade(f, f.BothApplicant, "Web Development", score: 9, max: 10, approved: true);
        var model = f.DashboardAs(AppRoles.Editor, WebDevId);

        await model.OnGetAsync();

        Assert.Contains(model.RecentActivity, a => a.Description == "Ana Cruz submitted the exam");
        Assert.Contains(model.RecentActivity, a => a.Description == "Cai Eve submitted the exam");
        Assert.Contains(model.RecentActivity, a => a.Description == "Noah Reyes approved Cai Eve's attempt");
        Assert.Equal(model.RecentActivity.OrderByDescending(a => a.When), model.RecentActivity);
    }

    [Fact]
    public async Task Dashboard_RecentActivityIgnoresOtherDepartmentsAndUntakenExams()
    {
        var f = new Fixture();
        Grade(f, f.PhotoApplicant, "Photo", score: 9, max: 10, approved: true);
        var model = f.DashboardAs(AppRoles.Editor, WebDevId);

        await model.OnGetAsync();

        Assert.Empty(model.RecentActivity);
        Assert.Empty(model.Attention);
    }
}

