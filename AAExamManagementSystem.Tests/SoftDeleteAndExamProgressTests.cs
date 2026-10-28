using AAExamManagementSystem.Hubs;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Pages.Attempts;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using AppRoles = AAExamManagementSystem.Models.Entities.Roles;

namespace AAExamManagementSystem.Tests;

public class SoftDeleteAndExamProgressTests
{
    private const string UserId = "applicant-user";

    private sealed class Fixture
    {
        public ApplicationDbContext Db { get; } = TestSupport.CreateContext();
        public Section Web { get; } = new() { Name = "Web Development" };
        public Question Question { get; private set; } = null!;
        public Choice RightChoice { get; } = new() { ChoiceText = "Right", IsCorrect = true };
        public Choice WrongChoice { get; } = new() { ChoiceText = "Wrong" };
        public Choice RetiredChoice { get; } = new() { ChoiceText = "Retired", IsActive = false };
        public Applicant Applicant { get; private set; } = null!;

        public Fixture(bool withSignupAttempt = true)
        {
            Db.QuestionTypes.Add(new QuestionType { Id = 1, Name = QuestionTypes.MultipleChoice });
            Db.Sections.Add(Web);
            Db.SaveChanges();

            Question = new Question { QuestionTitle = "Pick one", QuestionTypeId = 1, SectionId = Web.Id, Score = 2 };
            Db.Questions.Add(Question);
            foreach (var choice in new[] { RightChoice, WrongChoice, RetiredChoice })
            {
                Db.QuestionAndChoices.Add(new QuestionAndChoice { Question = Question, Choice = choice });
            }

            Applicant = new Applicant
            {
                FirstName = "Ana",
                LastName = "Cruz",
                StudentNo = "2026-1",
                Email = "ana@example.test",
                AcademicYear = "2026-2027",
                UserId = UserId,
            };
            if (withSignupAttempt)
            {
                Applicant.Attempts.Add(new Attempt { Section = Web.Name, ChoiceNumber = 1, Password = "pw-1" });
            }

            Db.Applicants.Add(Applicant);
            Db.SaveChanges();
        }

        // A new page model each time, like a request after an app restart; only the database carries over.
        public TakeModel TakeAsApplicant()
        {
            var userManager = TestSupport.CreateUserManager();
            userManager.Setup(m => m.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).Returns(UserId);

            var notifications = new NotificationService(
                Db, Mock.Of<IHubContext<NotificationHub>>(), Mock.Of<IMapper>(), userManager.Object);

            var model = new TakeModel(
                Db, userManager.Object, new QuestionChoiceService(Db), notifications, NullLogger<TakeModel>.Instance);
            TestSupport.Attach(model, TestSupport.PrincipalFor(AppRoles.Applicant));
            return model;
        }
    }

    [Fact]
    public async Task Sections_DefaultQueryHidesInactiveOnes()
    {
        var f = new Fixture();
        f.Db.Sections.Add(new Section { Name = "Retired", IsActive = false });
        await f.Db.SaveChangesAsync();

        var names = await f.Db.Sections.Select(s => s.Name).ToListAsync();

        Assert.Equal(new[] { "Web Development" }, names);
    }

    [Fact]
    public async Task Repository_GetAllIncludingInactive_StillReturnsThem()
    {
        var f = new Fixture();
        f.Db.Sections.Add(new Section { Name = "Retired", IsActive = false });
        await f.Db.SaveChangesAsync();
        var repository = new GenericRepository<Section>(f.Db);

        var active = await repository.GetAllAsync();
        var everything = await repository.GetAllIncludingInactiveAsync();

        Assert.Single(active);
        Assert.Equal(2, everything.Count());
    }

    [Fact]
    public async Task Repository_GetById_FindsAnInactiveRowSoAdminsCanReactivateIt()
    {
        var f = new Fixture();
        var retired = new Section { Name = "Retired", IsActive = false };
        f.Db.Sections.Add(retired);
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();

        var found = await new GenericRepository<Section>(f.Db).GetByIdAsync(retired.Id);

        Assert.NotNull(found);
        Assert.False(found!.IsActive);
    }

    [Fact]
    public async Task Departments_AndCourses_AreFilteredToo()
    {
        var f = new Fixture();
        f.Db.Departments.AddRange(new Department { Name = "Live" }, new Department { Name = "Gone", IsActive = false });
        f.Db.Courses.AddRange(new Course { Name = "Live" }, new Course { Name = "Gone", IsActive = false });
        await f.Db.SaveChangesAsync();

        Assert.Equal("Live", Assert.Single(await f.Db.Departments.ToListAsync()).Name);
        Assert.Equal("Live", Assert.Single(await f.Db.Courses.ToListAsync()).Name);
        Assert.Equal(2, await f.Db.Departments.IgnoreQueryFilters().CountAsync());
        Assert.Equal(2, await f.Db.Courses.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task GradedAnswers_StillResolveTheirQuestionAfterTheSectionIsDeactivated()
    {
        var f = new Fixture();
        var attempt = f.Db.Attempts.Single();
        attempt.DateTaken = DateTime.UtcNow.ToString("o");
        attempt.Answers.Add(new Answer { ApplicantId = f.Applicant.Id, QuestionId = f.Question.Id, Point = 2 });
        f.Web.IsActive = false;
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();

        var loaded = await f.Db.Attempts
            .Include(a => a.Answers)
            .ThenInclude(ans => ans.Question)
            .SingleAsync();

        Assert.Equal(2, loaded.Answers.Sum(ans => ans.Question.Score));
    }

    [Fact]
    public async Task Choices_ForAnApplicant_LeaveOutDeactivatedOnes()
    {
        var f = new Fixture();
        var service = new QuestionChoiceService(f.Db);

        var forApplicant = await service.GetChoicesAsync(f.Question.Id, activeOnly: true);
        var forAdmin = await service.GetChoicesAsync(f.Question.Id);

        Assert.DoesNotContain(forApplicant, c => c.ChoiceText == "Retired");
        Assert.Equal(2, forApplicant.Count);
        Assert.Contains(forAdmin, c => c.ChoiceText == "Retired");
    }

    [Fact]
    public async Task ExamPage_NeverOffersADeactivatedChoice()
    {
        var f = new Fixture();
        var model = f.TakeAsApplicant();

        await model.OnGetAsync();

        var question = Assert.Single(model.CurrentPageQuestions);
        Assert.DoesNotContain(question.Choices, c => c.ChoiceText == "Retired");
    }

    [Fact]
    public async Task Progress_SurvivesARestart()
    {
        var f = new Fixture();
        var first = f.TakeAsApplicant();
        await first.OnGetAsync();
        first.Answers[f.Question.Id] = f.WrongChoice.Id;

        await first.OnPostAsync(1, "next");

        var afterRestart = f.TakeAsApplicant();
        await afterRestart.OnGetAsync();

        Assert.Equal(f.WrongChoice.Id, afterRestart.Answers[f.Question.Id]);
    }

    [Fact]
    public async Task Progress_IsStoredOnTheAttemptNotInTheSession()
    {
        var f = new Fixture();
        var model = f.TakeAsApplicant();
        await model.OnGetAsync();
        model.Answers[f.Question.Id] = f.RightChoice.Id;

        await model.OnPostAsync(1, "next");

        var attempt = await f.Db.Attempts.AsNoTracking().SingleAsync();
        Assert.NotNull(attempt.ProgressJson);
        Assert.Contains(f.RightChoice.Id.ToString(), attempt.ProgressJson);
        Assert.Null(attempt.DateTaken);
    }

    [Fact]
    public async Task ApplicantWithoutAnAttempt_HasNoExamToTake()
    {
        var f = new Fixture(withSignupAttempt: false);
        var model = f.TakeAsApplicant();

        await model.OnGetAsync();

        Assert.Equal(0, model.TotalPages);
        Assert.Empty(f.Db.Attempts);
    }

    [Fact]
    public async Task Exam_StaysClosedUntilItsBookedSlotStarts()
    {
        var f = new Fixture();
        f.Applicant.Attempts.Single().SetSchedule(DateOnly.FromDateTime(DateTime.Now.AddDays(1)), new TimeOnly(8, 0));
        f.Db.SaveChanges();
        var model = f.TakeAsApplicant();

        await model.OnGetAsync();
        model.Answers[f.Question.Id] = f.RightChoice.Id;
        await model.OnPostAsync(1, "next");

        Assert.NotNull(model.NotOpenUntil);
        Assert.Equal(0, model.TotalPages);
        Assert.Null((await f.Db.Attempts.AsNoTracking().SingleAsync()).ProgressJson);
    }

    [Fact]
    public async Task Exam_OpensOnceItsBookedSlotHasStarted()
    {
        var f = new Fixture();
        f.Applicant.Attempts.Single().SetSchedule(DateOnly.FromDateTime(DateTime.Now.AddDays(-1)), new TimeOnly(8, 0));
        f.Db.SaveChanges();
        var model = f.TakeAsApplicant();

        await model.OnGetAsync();

        Assert.Null(model.NotOpenUntil);
        Assert.True(model.TotalPages > 0);
    }

    [Fact]
    public async Task Submitting_GradesFromTheSavedProgressAndClearsIt()
    {
        var f = new Fixture();
        var model = f.TakeAsApplicant();
        await model.OnGetAsync();
        model.Answers[f.Question.Id] = f.RightChoice.Id;

        var result = await model.OnPostAsync(1, "submit");

        Assert.IsType<RedirectToPageResult>(result);
        var attempt = await f.Db.Attempts.AsNoTracking().Include(a => a.Answers).SingleAsync();
        Assert.NotNull(attempt.DateTaken);
        Assert.Equal(2, attempt.TotalScore);
        Assert.Null(attempt.ProgressJson);
        Assert.Single(attempt.Answers);
    }

    [Fact]
    public async Task Progress_IsKeptPerAttempt_NotSharedBetweenExams()
    {
        var f = new Fixture();
        var model = f.TakeAsApplicant();
        await model.OnGetAsync();
        model.Answers[f.Question.Id] = f.WrongChoice.Id;
        await model.OnPostAsync(1, "next");

        f.Applicant.Attempts.Add(new Attempt { Section = "Other", Password = "pw-2" });
        await f.Db.SaveChangesAsync();

        var other = await f.Db.Attempts.AsNoTracking().SingleAsync(a => a.Section == "Other");
        Assert.Null(other.ProgressJson);
    }
}
