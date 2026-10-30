using AAExamManagementSystem.Data;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AppRoles = AAExamManagementSystem.Models.Entities.Roles;

namespace AAExamManagementSystem.Tests;

// Runs the real seeder once against an in-memory database and checks the exams, ordering and questions it builds.
public class SeededExamDataFixture : IAsyncLifetime
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    public ServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Services = BuildServices(_databaseName);
        await SeedAsync();
    }

    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        await IdentitySeeder.SeedAsync(scope.ServiceProvider);
    }

    public ApplicationDbContext NewContext() => Services.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();

    public Task DisposeAsync()
    {
        Services.Dispose();
        return Task.CompletedTask;
    }

    private static ServiceProvider BuildServices(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddIdentity<ApplicationUser, ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        return services.BuildServiceProvider();
    }
}

public class SeededExamDataTests : IClassFixture<SeededExamDataFixture>
{
    private readonly SeededExamDataFixture _fixture;

    public SeededExamDataTests(SeededExamDataFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed record Snapshot(
        List<ExamPage> Pages,
        List<PageSection> Sections,
        List<Question> Questions,
        Dictionary<int, string> DepartmentNames,
        Dictionary<int, string> TypeNames);

    private async Task<Snapshot> LoadAsync()
    {
        await using var db = _fixture.NewContext();
        return new Snapshot(
            await db.ExamPages.AsNoTracking().ToListAsync(),
            await db.PageSections.AsNoTracking().ToListAsync(),
            await db.Questions.AsNoTracking().ToListAsync(),
            await db.Sections.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name),
            await db.QuestionTypes.AsNoTracking().ToDictionaryAsync(qt => qt.Id, qt => qt.Name));
    }

    [Fact]
    public async Task EveryDepartmentRoleAndQuestionTypeIsSeeded()
    {
        var data = await LoadAsync();

        Assert.Equal(Departments.All.Order(), data.DepartmentNames.Values.Order());
        Assert.Equal(QuestionTypes.All.Order(), data.TypeNames.Values.Order());

        await using var db = _fixture.NewContext();
        Assert.Equal(AppRoles.All.Order(), db.Roles.Select(r => r.Name!).AsEnumerable().Order());
    }

    [Fact]
    public async Task TheAdminAccountIsSeededInTheAdminRole()
    {
        using var scope = _fixture.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var admin = await userManager.FindByNameAsync("admin");

        Assert.NotNull(admin);
        Assert.True(await userManager.IsInRoleAsync(admin!, AppRoles.Admin));
        Assert.True(await userManager.CheckPasswordAsync(admin!, "P@ssword2026!"));
    }

    [Theory]
    [InlineData("wdeditor", Departments.WebDevelopment)]
    [InlineData("adeditor", Departments.ArtAndDesign)]
    public async Task DepartmentEditorsAreInTheEditorRoleAndTheirDepartment(string userName, string department)
    {
        using var scope = _fixture.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var editor = await userManager.FindByNameAsync(userName);

        Assert.NotNull(editor);
        Assert.True(await userManager.IsInRoleAsync(editor!, AppRoles.Editor));
        Assert.Equal(department, (await db.Sections.FindAsync(editor!.SectionId))!.Name);
    }

    [Theory]
    [InlineData(Departments.GeneralKnowledge)]
    [InlineData(Departments.Video)]
    [InlineData(Departments.CustomerSupport)]
    [InlineData(Departments.Marketing)]
    public async Task DepartmentsWithAPlannedExamHaveExamPages(string department)
    {
        var data = await LoadAsync();
        var departmentId = data.DepartmentNames.Single(d => d.Value == department).Key;

        Assert.Contains(data.Pages, p => p.SectionId == departmentId);
    }

    [Fact]
    public async Task PageNumbersWithinADepartmentAreConsecutiveFromOne()
    {
        var data = await LoadAsync();

        foreach (var department in data.Pages.GroupBy(p => p.SectionId))
        {
            var numbers = department.Select(p => p.PageNumber).Order().ToList();
            Assert.Equal(Enumerable.Range(1, numbers.Count), numbers);
        }
    }

    [Fact]
    public async Task SectionOrderWithinAPageIsUniqueAndStartsAtOne()
    {
        var data = await LoadAsync();

        foreach (var page in data.Sections.GroupBy(ps => ps.ExamPageId))
        {
            var orders = page.Select(ps => ps.Order).Order().ToList();
            Assert.Equal(orders.Distinct(), orders);
            Assert.Equal(1, orders.First());
        }
    }

    [Fact]
    public async Task QuestionOrderWithinASectionIsConsecutiveFromOne()
    {
        var data = await LoadAsync();

        foreach (var section in data.Questions.Where(q => q.PageSectionId != null).GroupBy(q => q.PageSectionId))
        {
            var orders = section.Select(q => q.PageOrder).Order().ToList();
            Assert.Equal(Enumerable.Range(1, orders.Count), orders);
        }
    }

    [Fact]
    public async Task PlacedQuestionsBelongToTheDepartmentAndTypeOfTheirSection()
    {
        var data = await LoadAsync();
        var sectionsById = data.Sections.ToDictionary(ps => ps.Id);
        var pagesById = data.Pages.ToDictionary(p => p.Id);

        foreach (var question in data.Questions.Where(q => q.PageSectionId != null))
        {
            var section = sectionsById[question.PageSectionId!.Value];
            var page = pagesById[section.ExamPageId];

            Assert.True(question.SectionId == page.SectionId,
                $"'{question.QuestionTitle}' sits on a {data.DepartmentNames[page.SectionId]} page but belongs to {data.DepartmentNames[question.SectionId]}.");
            Assert.True(question.QuestionTypeId == section.QuestionTypeId,
                $"'{question.QuestionTitle}' is {data.TypeNames[question.QuestionTypeId]} in a {data.TypeNames[section.QuestionTypeId]} section.");
        }
    }

    [Fact]
    public async Task NoExamPageIsEmpty()
    {
        var data = await LoadAsync();
        var populatedSections = data.Questions.Where(q => q.PageSectionId != null).Select(q => q.PageSectionId!.Value).ToHashSet();

        Assert.All(data.Sections, ps => Assert.Contains(ps.Id, populatedSections));
        Assert.All(data.Pages, p => Assert.Contains(data.Sections, ps => ps.ExamPageId == p.Id));
    }

    [Fact]
    public async Task EveryQuestionHasATitleAScoreAndAKnownDepartmentAndType()
    {
        var data = await LoadAsync();

        Assert.NotEmpty(data.Questions);
        foreach (var question in data.Questions)
        {
            Assert.False(string.IsNullOrWhiteSpace(question.QuestionTitle));
            Assert.True(question.Score > 0, $"'{question.QuestionTitle}' has no score.");
            Assert.True(data.DepartmentNames.ContainsKey(question.SectionId));
            Assert.True(data.TypeNames.ContainsKey(question.QuestionTypeId));
        }
    }

    [Fact]
    public async Task QuestionsAreUniqueWithinADepartmentByTitleAndImage()
    {
        var data = await LoadAsync();

        // Image questions share a title ("Identify the logo shown...") and differ by their picture.
        var duplicates = data.Questions
            .GroupBy(q => (q.SectionId, Title: q.QuestionTitle.Trim().ToLowerInvariant(), q.Image))
            .Where(g => g.Count() > 1)
            .Select(g => $"{data.DepartmentNames[g.Key.SectionId]}: {g.First().QuestionTitle}")
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public async Task EveryMultipleChoiceQuestionHasAtLeastTwoChoicesAndExactlyOneCorrectOne()
    {
        await using var db = _fixture.NewContext();
        var multipleChoiceId = (await db.QuestionTypes.SingleAsync(qt => qt.Name == QuestionTypes.MultipleChoice)).Id;
        var links = await db.QuestionAndChoices.Include(qc => qc.Choice).AsNoTracking().ToListAsync();
        var questions = await db.Questions.AsNoTracking().Where(q => q.QuestionTypeId == multipleChoiceId).ToListAsync();

        Assert.NotEmpty(questions);
        foreach (var question in questions)
        {
            var choices = links.Where(l => l.QuestionId == question.Id).Select(l => l.Choice).ToList();
            Assert.True(choices.Count >= 2, $"'{question.QuestionTitle}' has {choices.Count} choice(s).");
            Assert.True(choices.Count(c => c.IsCorrect) == 1,
                $"'{question.QuestionTitle}' has {choices.Count(c => c.IsCorrect)} correct choices.");
        }
    }

    [Fact]
    public async Task EssayQuestionsHaveNoChoices()
    {
        await using var db = _fixture.NewContext();
        var essayId = (await db.QuestionTypes.SingleAsync(qt => qt.Name == QuestionTypes.Essay)).Id;
        var essayIds = await db.Questions.Where(q => q.QuestionTypeId == essayId).Select(q => q.Id).ToListAsync();

        Assert.NotEmpty(essayIds);
        Assert.DoesNotContain(db.QuestionAndChoices, qc => essayIds.Contains(qc.QuestionId));
    }

    [Fact]
    public async Task ContentDevelopmentHasEightTwoPointWordUsageIdentificationQuestions()
    {
        await using var db = _fixture.NewContext();
        var identificationId = (await db.QuestionTypes.SingleAsync(qt => qt.Name == QuestionTypes.Identification)).Id;
        var sectionId = (await db.Sections.SingleAsync(s => s.Name == Departments.ContentDevelopment)).Id;

        var questions = await db.Questions
            .Where(q => q.SectionId == sectionId && q.QuestionTypeId == identificationId && q.QuestionTitle.StartsWith("Type the correct word"))
            .ToListAsync();

        Assert.Equal(8, questions.Count);
        Assert.All(questions, q =>
        {
            Assert.Equal(2, q.Score);
            Assert.Single(db.QuestionAndChoices.Where(qc => qc.QuestionId == q.Id));
        });
    }

    [Fact]
    public async Task SeedingAgainDoesNotDuplicateQuestionsPagesOrUsers()
    {
        await using (var before = _fixture.NewContext())
        {
            var counts = (
                Questions: await before.Questions.CountAsync(),
                Pages: await before.ExamPages.CountAsync(),
                Sections: await before.PageSections.CountAsync(),
                Choices: await before.Choices.CountAsync(),
                Users: await before.Users.CountAsync(),
                Departments: await before.Sections.CountAsync());

            await _fixture.SeedAsync();

            await using var after = _fixture.NewContext();
            Assert.Equal(counts.Questions, await after.Questions.CountAsync());
            Assert.Equal(counts.Pages, await after.ExamPages.CountAsync());
            Assert.Equal(counts.Sections, await after.PageSections.CountAsync());
            Assert.Equal(counts.Choices, await after.Choices.CountAsync());
            Assert.Equal(counts.Users, await after.Users.CountAsync());
            Assert.Equal(counts.Departments, await after.Sections.CountAsync());
        }
    }
}

public class QuestionTypesTests
{
    [Theory]
    [InlineData("Multiple Choice", true)]
    [InlineData("multiple choice", true)]
    [InlineData("MULTIPLE CHOICE", true)]
    [InlineData("Essay", false)]
    [InlineData("Identification", false)]
    [InlineData("True or False", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void RequiresChoices_IsOnlyTrueForMultipleChoice(string? name, bool expected)
    {
        Assert.Equal(expected, QuestionTypes.RequiresChoices(name));
    }

    [Fact]
    public void AllListsEveryTypeOnce()
    {
        Assert.Equal(QuestionTypes.All.Length, QuestionTypes.All.Distinct().Count());
        Assert.Contains(QuestionTypes.MultipleChoice, QuestionTypes.All);
        Assert.Contains(QuestionTypes.Essay, QuestionTypes.All);
    }
}
