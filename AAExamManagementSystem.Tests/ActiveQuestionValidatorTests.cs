using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AAExamManagementSystem.Tests;

public class ActiveQuestionValidatorTests
{
    private const int Web = 1;
    private const int Data = 2;

    private static Question Make(int sectionId, string title, bool active = true, bool deleted = false) =>
        new() { QuestionTypeId = 1, SectionId = sectionId, QuestionTitle = title, IsActive = active, IsDeleted = deleted };

    [Theory]
    [InlineData("What is HTML?")]
    [InlineData("what is html?")]
    [InlineData("  WHAT IS HTML?  ")]
    public async Task AnActiveQuestionWithTheSameTitle_BlocksIt_IgnoringCaseAndEdgeSpaces(string title)
    {
        var db = TestSupport.CreateContext();
        db.Questions.Add(Make(Web, "What is HTML?"));
        db.SaveChanges();

        Assert.False(await new ActiveQuestionValidator(db).IsAvailableAsync(Web, title));
    }

    [Fact]
    public async Task AnInactiveQuestionDoesNotBlockTheTitle()
    {
        var db = TestSupport.CreateContext();
        db.Questions.Add(Make(Web, "What is HTML?", active: false));
        db.SaveChanges();

        Assert.True(await new ActiveQuestionValidator(db).IsAvailableAsync(Web, "What is HTML?"));
    }

    [Fact]
    public async Task ADeletedQuestionDoesNotBlockTheTitle()
    {
        var db = TestSupport.CreateContext();
        db.Questions.Add(Make(Web, "What is HTML?", deleted: true));
        db.SaveChanges();

        Assert.True(await new ActiveQuestionValidator(db).IsAvailableAsync(Web, "What is HTML?"));
    }

    [Fact]
    public async Task TheSameTitleInAnotherSectionIsFine()
    {
        var db = TestSupport.CreateContext();
        db.Questions.Add(Make(Web, "What is a key?"));
        db.SaveChanges();

        Assert.True(await new ActiveQuestionValidator(db).IsAvailableAsync(Data, "What is a key?"));
    }

    [Fact]
    public async Task AQuestionDoesNotBlockItself()
    {
        var db = TestSupport.CreateContext();
        var question = Make(Web, "What is HTML?");
        db.Questions.Add(question);
        db.SaveChanges();
        var validator = new ActiveQuestionValidator(db);

        Assert.True(await validator.IsAvailableAsync(Web, "What is HTML?", question.Id));
        Assert.False(await validator.IsAvailableAsync(Web, "What is HTML?", Guid.NewGuid()));
    }

    [Fact]
    public async Task ABlankTitleIsLeftToTheRequiredRule()
    {
        var db = TestSupport.CreateContext();

        Assert.True(await new ActiveQuestionValidator(db).IsAvailableAsync(Web, "   "));
    }

    [Fact]
    public async Task Validate_AddsAnErrorUnderTheGivenKey_WhenTheQuestionWouldBeActive()
    {
        var db = TestSupport.CreateContext();
        db.Questions.Add(Make(Web, "What is HTML?"));
        db.SaveChanges();
        var modelState = new ModelStateDictionary();

        var ok = await new ActiveQuestionValidator(db)
            .ValidateAsync(modelState, "NewQuestion.QuestionTitle", Web, "what is html?", isActive: true);

        Assert.False(ok);
        Assert.Equal(
            "An active question titled 'what is html?' already exists in this section.",
            modelState["NewQuestion.QuestionTitle"]!.Errors.Single().ErrorMessage);
    }

    [Fact]
    public async Task Validate_LetsAnInactiveQuestionThrough_EvenWithATakenTitle()
    {
        var db = TestSupport.CreateContext();
        db.Questions.Add(Make(Web, "What is HTML?"));
        db.SaveChanges();
        var modelState = new ModelStateDictionary();

        var ok = await new ActiveQuestionValidator(db)
            .ValidateAsync(modelState, "NewQuestion.QuestionTitle", Web, "What is HTML?", isActive: false);

        Assert.True(ok);
        Assert.True(modelState.IsValid);
    }
}
