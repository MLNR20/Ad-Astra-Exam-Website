using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Pages;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AAExamManagementSystem.Tests;

public class SignupChoiceTests
{
    private sealed class Fixture
    {
        public ApplicationDbContext Db { get; } = TestSupport.CreateContext();
        public Mock<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>> UserManager { get; } = TestSupport.CreateUserManager();

        public Fixture()
        {
            Db.QuestionTypes.Add(new QuestionType { Id = 1, Name = QuestionTypes.MultipleChoice });
            AddSection("Web Development", withQuestion: true);
            AddSection("Marketing", withQuestion: true);
            AddSection(Departments.GeneralKnowledge, withQuestion: true);
            AddSection("Empty Department", withQuestion: false);
            AddSection("Retired Department", withQuestion: true, isActive: false);
            Db.SaveChanges();
        }

        private void AddSection(string name, bool withQuestion, bool isActive = true)
        {
            var section = new Section { Name = name, IsActive = isActive };
            if (withQuestion)
            {
                section.Questions.Add(new Question { QuestionTitle = $"{name} question", QuestionTypeId = 1 });
            }

            Db.Sections.Add(section);
        }

        public SignupModel Page(string? firstChoice = null, string? secondChoice = null)
        {
            var model = new SignupModel(
                UserManager.Object,
                TestSupport.CreateSignInManager(UserManager.Object).Object,
                Mock.Of<IGenericRepository<Applicant>>(),
                Mock.Of<IMapper>(),
                Db,
                Mock.Of<IRegistrationNotifier>())
            {
                Input = new ApplicantRegisterDto { FirstChoice = firstChoice, SecondChoice = secondChoice },
            };
            TestSupport.Attach(model, TestSupport.PrincipalFor());
            return model;
        }
    }

    [Fact]
    public async Task Options_AreActiveDepartmentsWithQuestionsExceptGeneralKnowledge()
    {
        var page = new Fixture().Page();

        await page.OnGetAsync();

        Assert.Equal(new[] { "Marketing", "Web Development" }, page.ChoiceOptions);
    }

    [Fact]
    public async Task Post_WithAChoiceThatIsNotInTheList_IsRejected()
    {
        var page = new Fixture().Page(firstChoice: "Underwater Basket Weaving");

        var result = await page.OnPostAsync();

        Assert.IsType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
        Assert.True(page.ModelState.ContainsKey("Input.FirstChoice"));
        Assert.Equal("Please pick a department from the list.", page.ModelState["Input.FirstChoice"]!.Errors.Single().ErrorMessage);
    }

    [Theory]
    [InlineData("General Knowledge")]
    [InlineData("Empty Department")]
    [InlineData("Retired Department")]
    public async Task Post_WithADepartmentThatCannotBeChosen_IsRejected(string choice)
    {
        var page = new Fixture().Page(secondChoice: choice);

        await page.OnPostAsync();

        Assert.True(page.ModelState.ContainsKey("Input.SecondChoice"));
    }

    [Fact]
    public async Task Post_WithTheSameDepartmentTwice_IsRejected()
    {
        var page = new Fixture().Page("Marketing", "marketing");

        await page.OnPostAsync();

        Assert.Equal("Pick a different department for your second choice.",
            page.ModelState["Input.SecondChoice"]!.Errors.Single().ErrorMessage);
    }

    [Fact]
    public async Task Post_WithValidChoices_PassesChoiceValidationAndUsesTheListedSpelling()
    {
        var page = new Fixture().Page("web development", "Marketing");
        page.ModelState.AddModelError(string.Empty, "stop after choice validation");

        await page.OnPostAsync();

        Assert.False(page.ModelState.ContainsKey("Input.FirstChoice"));
        Assert.False(page.ModelState.ContainsKey("Input.SecondChoice"));
        Assert.Equal("Web Development", page.Input.FirstChoice);
    }

    [Fact]
    public async Task Post_WithNoChoices_DoesNotComplainAboutThem()
    {
        var page = new Fixture().Page();
        page.ModelState.AddModelError(string.Empty, "stop after choice validation");

        await page.OnPostAsync();

        Assert.False(page.ModelState.ContainsKey("Input.FirstChoice"));
        Assert.False(page.ModelState.ContainsKey("Input.SecondChoice"));
    }
}
