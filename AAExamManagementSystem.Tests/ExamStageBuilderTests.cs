using AAExamManagementSystem.Hubs;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Pages.Attempts;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using AppRoles = AAExamManagementSystem.Models.Entities.Roles;

namespace AAExamManagementSystem.Tests;

public class ExamStageBuilderTests
{
    private const string Web = "Web Development";
    private const string GeneralKnowledge = Departments.GeneralKnowledge;
    private const int MultipleChoiceId = 1;
    private const int IdentificationId = 2;
    private const int EssayId = 3;

    private sealed class Fixture
    {
        public ApplicationDbContext Db { get; } = TestSupport.CreateContext();
        public Section WebSection { get; } = new() { Name = Web };
        public Section GeneralSection { get; } = new() { Name = GeneralKnowledge };
        private int _minutes;

        public Fixture()
        {
            Db.QuestionTypes.AddRange(
                new QuestionType { Id = MultipleChoiceId, Name = QuestionTypes.MultipleChoice },
                new QuestionType { Id = IdentificationId, Name = QuestionTypes.Identification },
                new QuestionType { Id = EssayId, Name = QuestionTypes.Essay });
            Db.Sections.AddRange(WebSection, GeneralSection);
            Db.SaveChanges();
        }

        // Each question is created a minute after the one before it, so DateCreated gives a stable order.
        public Question Question(Section section, string title, int typeId = MultipleChoiceId, string? image = null)
        {
            var question = new Question
            {
                QuestionTitle = title,
                SectionId = section.Id,
                QuestionTypeId = typeId,
                Image = image,
                DateCreated = new DateTime(2026, 1, 1).AddMinutes(_minutes++),
            };
            Db.Questions.Add(question);
            Db.SaveChanges();
            return question;
        }

        public PageSection Page(Section section, int pageNumber, int sectionOrder, int typeId, string? pageInstruction = null,
            string? sectionInstruction = null, params (Question Question, int Order)[] placed)
        {
            var page = Db.ExamPages.FirstOrDefault(p => p.SectionId == section.Id && p.PageNumber == pageNumber)
                ?? Db.ExamPages.Add(new ExamPage { SectionId = section.Id, PageNumber = pageNumber, Instruction = pageInstruction }).Entity;
            var pageSection = new PageSection { ExamPage = page, QuestionTypeId = typeId, Order = sectionOrder, Instruction = sectionInstruction };
            Db.PageSections.Add(pageSection);
            foreach (var (question, order) in placed)
            {
                question.PageSection = pageSection;
                question.PageOrder = order;
            }

            Db.SaveChanges();
            return pageSection;
        }

        public Task<IList<ExamStage>> BuildAsync(string department = Web) => new ExamStageBuilder(Db).BuildAsync(department);

        public TakeModel TakeAsApplicant(string choice = Web)
        {
            const string userId = "applicant-user";
            var applicant = new Applicant
            {
                FirstName = "Ana", LastName = "Cruz", StudentNo = "1", Email = "ana@example.test",
                AcademicYear = "2026-2027", UserId = userId, FirstChoice = choice,
            };
            applicant.Attempts.Add(new Attempt { Section = choice, Password = "pw" });
            Db.Applicants.Add(applicant);
            Db.SaveChanges();

            var userManager = TestSupport.CreateUserManager();
            userManager.Setup(m => m.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).Returns(userId);
            var notifications = new NotificationService(Db, Mock.Of<IHubContext<NotificationHub>>(), Mock.Of<IMapper>(), userManager.Object);
            var model = new TakeModel(Db, userManager.Object, new QuestionChoiceService(Db), notifications, NullLogger<TakeModel>.Instance);
            TestSupport.Attach(model, TestSupport.PrincipalFor(AppRoles.Applicant));
            return model;
        }
    }

    [Fact]
    public async Task TheDepartmentsPagesComeFirstThenGeneralKnowledge()
    {
        var f = new Fixture();
        var gk = f.Question(f.GeneralSection, "GK question");
        var second = f.Question(f.WebSection, "Second page");
        var first = f.Question(f.WebSection, "First page");
        f.Page(f.GeneralSection, 1, 1, MultipleChoiceId, placed: new[] { (gk, 1) });
        f.Page(f.WebSection, 2, 1, MultipleChoiceId, placed: new[] { (second, 1) });
        f.Page(f.WebSection, 1, 1, MultipleChoiceId, placed: new[] { (first, 1) });

        var stages = await f.BuildAsync();

        Assert.Equal(new[] { "First page", "Second page", "GK question" }, stages.Select(s => s.Questions.Single().QuestionTitle));
        Assert.Equal(new[] { Web, Web, GeneralKnowledge }, stages.Select(s => s.Title));
        Assert.Equal(new[] { Web, Web, GeneralKnowledge }, stages.Select(s => s.HeroDepartment));
    }

    [Fact]
    public async Task SectionsAndQuestionsFollowTheirSetOrderWithinAPage()
    {
        var f = new Fixture();
        var a1 = f.Question(f.WebSection, "A first");
        var a2 = f.Question(f.WebSection, "A second");
        var b1 = f.Question(f.WebSection, "B first", EssayId);
        var b2 = f.Question(f.WebSection, "B second", EssayId);
        f.Page(f.WebSection, 1, 2, EssayId, placed: new[] { (b2, 2), (b1, 1) });
        f.Page(f.WebSection, 1, 1, MultipleChoiceId, placed: new[] { (a2, 2), (a1, 1) });

        var stage = Assert.Single(await f.BuildAsync());

        Assert.Equal(new[] { "A first", "A second", "B first", "B second" }, stage.Questions.Select(q => q.QuestionTitle));
    }

    [Fact]
    public async Task ASectionsInstructionSitsOnItsFirstQuestion()
    {
        var f = new Fixture();
        var first = f.Question(f.WebSection, "First");
        var second = f.Question(f.WebSection, "Second");
        f.Page(f.WebSection, 1, 1, MultipleChoiceId, sectionInstruction: "Pick one", placed: new[] { (second, 2), (first, 1) });

        var stage = Assert.Single(await f.BuildAsync());

        var heading = Assert.Single(stage.SectionHeadings);
        Assert.Equal(first.Id, heading.Key);
        Assert.Equal("Pick one", heading.Value.Instruction);
    }

    [Fact]
    public async Task APagesInstructionBecomesTheHeadingAndABlankOneIsLeftForTheDefault()
    {
        var f = new Fixture();
        var q1 = f.Question(f.WebSection, "On page one");
        var q2 = f.Question(f.WebSection, "On page two");
        f.Page(f.WebSection, 1, 1, MultipleChoiceId, pageInstruction: "Part A", placed: new[] { (q1, 1) });
        f.Page(f.WebSection, 2, 1, MultipleChoiceId, pageInstruction: "  ", placed: new[] { (q2, 1) });

        var stages = await f.BuildAsync();

        Assert.Equal("Part A", stages[0].Heading);
        Assert.Null(stages[1].Heading);
    }

    [Fact]
    public async Task InactiveQuestionsSectionsAndEmptyPagesAreLeftOut()
    {
        var f = new Fixture();
        var kept = f.Question(f.WebSection, "Kept");
        var inactive = f.Question(f.WebSection, "Inactive question");
        var hidden = f.Question(f.WebSection, "In an inactive section");
        var lonely = f.Question(f.WebSection, "Alone on a page that is switched off");
        f.Page(f.WebSection, 1, 1, MultipleChoiceId, placed: new[] { (kept, 1), (inactive, 2) });
        var hiddenSection = f.Page(f.WebSection, 1, 2, MultipleChoiceId, placed: new[] { (hidden, 1) });
        f.Page(f.WebSection, 2, 1, MultipleChoiceId, placed: new[] { (lonely, 1) });
        inactive.IsActive = false;
        hiddenSection.IsActive = false;
        f.Db.ExamPages.Single(p => p.PageNumber == 2).IsActive = false;
        f.Db.SaveChanges();

        var stage = Assert.Single(await f.BuildAsync());

        Assert.Equal(new[] { "Kept" }, stage.Questions.Select(q => q.QuestionTitle));
    }

    [Fact]
    public async Task WithoutExamPagesTheDepartmentIsOnePageOfEveryQuestionWithNoCap()
    {
        var f = new Fixture();
        for (var i = 1; i <= 12; i++)
        {
            f.Question(f.WebSection, $"Question {i:00}", i % 2 == 0 ? IdentificationId : MultipleChoiceId);
        }

        var stage = Assert.Single(await f.BuildAsync());

        Assert.Equal(12, stage.Questions.Count);
        Assert.Equal("Question 01", stage.Questions.First().QuestionTitle);
        Assert.Equal("Question 12", stage.Questions.Last().QuestionTitle);
        Assert.Null(stage.Heading);
    }

    [Fact]
    public async Task WithoutExamPagesAnUnplacedEssayIsNotOffered()
    {
        var f = new Fixture();
        f.Question(f.WebSection, "Multiple choice");
        f.Question(f.WebSection, "Essay nobody placed", EssayId);

        var stage = Assert.Single(await f.BuildAsync());

        Assert.Equal(new[] { "Multiple choice" }, stage.Questions.Select(q => q.QuestionTitle));
    }

    [Fact]
    public async Task SqlAndDebuggingQuestionsGetTheirOwnPageAfterTheRest()
    {
        var f = new Fixture();
        f.Question(f.WebSection, "Plain", IdentificationId);
        f.Question(f.WebSection, "Write the query", IdentificationId, "/images/sql-schema-1.png");
        f.Question(f.WebSection, "Find the bug", IdentificationId, "/images/debug-1.png");

        var stages = await f.BuildAsync();

        Assert.Equal(2, stages.Count);
        Assert.Equal(new[] { "Plain" }, stages[0].Questions.Select(q => q.QuestionTitle));
        Assert.Equal("SQL Queries and Debugging", stages[1].Title);
        Assert.Equal("SQL Query and Debugging Questions", stages[1].Heading);
        Assert.Equal(new[] { "Write the query", "Find the bug" }, stages[1].Questions.Select(q => q.QuestionTitle));
        Assert.Equal(Web, stages[1].HeroDepartment);
    }

    [Fact]
    public async Task GeneralKnowledgeWithoutPagesIsAppendedAsOnePage()
    {
        var f = new Fixture();
        f.Question(f.WebSection, "Web question");
        f.Question(f.GeneralSection, "GK question");

        var stages = await f.BuildAsync();

        Assert.Equal(new[] { Web, GeneralKnowledge }, stages.Select(s => s.Title));
        Assert.Equal(GeneralKnowledge, stages[1].HeroDepartment);
    }

    [Fact]
    public async Task GeneralKnowledgeIsNotAddedTwiceToItsOwnExam()
    {
        var f = new Fixture();
        f.Question(f.GeneralSection, "GK question");

        var stages = await f.BuildAsync(GeneralKnowledge);

        Assert.Single(stages);
    }

    [Fact]
    public async Task AnUnknownOrInactiveDepartmentHasNoStages()
    {
        var f = new Fixture();
        f.Question(f.WebSection, "Web question");
        f.WebSection.IsActive = false;
        f.Db.SaveChanges();

        Assert.Empty(await f.BuildAsync("Nonexistent"));
        Assert.Empty(await f.BuildAsync(Web));
    }

    [Fact]
    public async Task TheApplicantsExamShowsEachPagesOwnHeroAndTheGeneralKnowledgePageLast()
    {
        var f = new Fixture();
        var web1 = f.Question(f.WebSection, "Web one");
        var web2 = f.Question(f.WebSection, "Web two");
        var gk = f.Question(f.GeneralSection, "GK one");
        f.Page(f.WebSection, 1, 1, MultipleChoiceId, placed: new[] { (web1, 1) });
        f.Page(f.WebSection, 2, 1, MultipleChoiceId, placed: new[] { (web2, 1) });
        f.Page(f.GeneralSection, 1, 1, MultipleChoiceId, placed: new[] { (gk, 1) });
        var model = f.TakeAsApplicant();

        await model.OnGetAsync(3);

        Assert.Equal(3, model.TotalPages);
        Assert.Equal(GeneralKnowledge, model.StageTitle);
        Assert.Equal(GeneralKnowledge, model.StageHeroDepartment);
        Assert.Equal(new[] { "GK one" }, model.CurrentPageQuestions.Select(q => q.QuestionTitle));

        await model.OnGetAsync(1);

        Assert.Equal(Web, model.StageTitle);
        Assert.Contains("Web Development", model.StageSubtitle);
    }

    [Fact]
    public async Task TheApplicantsExamAlsoSplitsOutTheSqlPageWhenThereAreNoExamPages()
    {
        var f = new Fixture();
        f.Question(f.WebSection, "Plain", IdentificationId);
        f.Question(f.WebSection, "Write the query", IdentificationId, "/images/sql-schema-1.png");
        var model = f.TakeAsApplicant();

        await model.OnGetAsync(2);

        Assert.Equal(2, model.TotalPages);
        Assert.Equal("SQL Queries", model.StageTitle);
        Assert.Equal("SQL Query Questions", model.CurrentPageHeading);
    }
}
