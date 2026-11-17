using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AAExamManagementSystem.Tests;

public class ActiveCorrectChoiceValidatorTests
{
    // A question with one active correct choice ("Right") and one plain choice ("Other").
    private sealed class Fixture
    {
        public ApplicationDbContext Db { get; } = TestSupport.CreateContext();
        public Question Question { get; } = new() { QuestionTitle = "Pick one" };
        public Choice Right { get; } = new() { ChoiceText = "Right", IsCorrect = true };
        public Choice Other { get; } = new() { ChoiceText = "Other" };
        public ActiveCorrectChoiceValidator Validator { get; }

        public Fixture()
        {
            Db.Questions.Add(Question);
            Db.QuestionAndChoices.AddRange(
                new QuestionAndChoice { Question = Question, Choice = Right },
                new QuestionAndChoice { Question = Question, Choice = Other });
            Db.SaveChanges();
            Validator = new ActiveCorrectChoiceValidator(Db);
        }
    }

    [Fact]
    public async Task ASecondActiveCorrectChoice_IsBlocked()
    {
        var f = new Fixture();

        Assert.False(await f.Validator.IsAllowedAsync(f.Other.Id, isCorrect: true, isActive: true));
    }

    [Fact]
    public async Task TheCorrectChoiceDoesNotBlockItself()
    {
        var f = new Fixture();

        Assert.True(await f.Validator.IsAllowedAsync(f.Right.Id, isCorrect: true, isActive: true));
    }

    [Fact]
    public async Task AnInactiveOrIncorrectChoiceNeverConflicts()
    {
        var f = new Fixture();

        Assert.True(await f.Validator.IsAllowedAsync(f.Other.Id, isCorrect: true, isActive: false));
        Assert.True(await f.Validator.IsAllowedAsync(f.Other.Id, isCorrect: false, isActive: true));
    }

    [Fact]
    public async Task AnInactiveCorrectChoiceDoesNotBlockAnother()
    {
        var f = new Fixture();
        f.Right.IsActive = false;
        f.Db.SaveChanges();

        Assert.True(await f.Validator.IsAllowedAsync(f.Other.Id, isCorrect: true, isActive: true));
    }

    [Fact]
    public async Task AnotherQuestionsCorrectChoiceDoesNotBlock()
    {
        var f = new Fixture();
        var second = new Question { QuestionTitle = "Another" };
        var theirs = new Choice { ChoiceText = "Theirs" };
        f.Db.Questions.Add(second);
        f.Db.QuestionAndChoices.Add(new QuestionAndChoice { Question = second, Choice = theirs });
        f.Db.SaveChanges();

        Assert.True(await f.Validator.IsAllowedAsync(theirs.Id, isCorrect: true, isActive: true));
    }

    [Fact]
    public async Task AChoiceNotLinkedToAQuestionYetNeverConflicts()
    {
        var f = new Fixture();
        var loose = new Choice { ChoiceText = "Loose" };
        f.Db.Choices.Add(loose);
        f.Db.SaveChanges();

        Assert.True(await f.Validator.IsAllowedAsync(loose.Id, isCorrect: true, isActive: true));
    }

    [Fact]
    public async Task Validate_AddsAnErrorUnderTheGivenKey()
    {
        var f = new Fixture();
        var modelState = new ModelStateDictionary();

        var ok = await f.Validator.ValidateAsync(modelState, "Choice.IsCorrect", f.Other.Id, isCorrect: true, isActive: true);

        Assert.False(ok);
        Assert.Equal(ActiveCorrectChoiceValidator.Message, modelState["Choice.IsCorrect"]!.Errors.Single().ErrorMessage);
    }
}
