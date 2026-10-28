using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AAExamManagementSystem.Tests;

public class ExamChoicePlannerTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 10, 0, 0);
    private static readonly string[] Options = { "Web Development", "Marketing" };

    private static ModelStateDictionary Validate(ExamChoicesDto input)
    {
        var modelState = new ModelStateDictionary();
        ExamChoicePlanner.Validate(modelState, input, Options, now: Now);
        return modelState;
    }

    private static ExamChoicesDto Valid() => new()
    {
        FirstChoice = "Web Development",
        FirstExamDate = new DateOnly(2026, 11, 3),
        FirstExamTime = new TimeOnly(8, 0),
        SecondChoice = "Marketing",
        SecondExamDate = new DateOnly(2026, 11, 3),
        SecondExamTime = new TimeOnly(9, 30),
    };

    [Fact]
    public void BothChoicesWithFutureSlots_AreAccepted()
    {
        Assert.True(Validate(Valid()).IsValid);
    }

    [Fact]
    public void ASecondChoiceWithNoSchedule_IsIgnored()
    {
        var input = Valid();
        input.SecondChoice = null;
        input.SecondExamDate = null;
        input.SecondExamTime = null;

        Assert.True(Validate(input).IsValid);
    }

    [Fact]
    public void AChosenDepartmentWithoutADateOrTime_IsRejected()
    {
        var input = Valid();
        input.FirstExamDate = null;
        input.FirstExamTime = null;

        var modelState = Validate(input);

        Assert.True(modelState.ContainsKey("Input.FirstExamDate"));
        Assert.True(modelState.ContainsKey("Input.FirstExamTime"));
    }

    [Theory]
    [InlineData(7, 0)]
    [InlineData(9, 0)]
    [InlineData(20, 0)]
    public void ATimeThatIsNotASlotStart_IsRejected(int hour, int minute)
    {
        var input = Valid();
        input.FirstExamTime = new TimeOnly(hour, minute);

        Assert.True(Validate(input).ContainsKey("Input.FirstExamTime"));
    }

    [Fact]
    public void ASlotThatHasAlreadyStarted_IsRejected()
    {
        var input = Valid();
        input.FirstExamDate = new DateOnly(2026, 11, 2);
        input.FirstExamTime = new TimeOnly(8, 0);

        Assert.True(Validate(input).ContainsKey("Input.FirstExamDate"));
    }

    [Fact]
    public void TwoExamsInTheSameSlot_AreRejected()
    {
        var input = Valid();
        input.SecondExamTime = input.FirstExamTime;

        Assert.True(Validate(input).ContainsKey("Input.SecondExamTime"));
    }

    [Fact]
    public void ADepartmentOutsideTheList_IsRejected()
    {
        var input = Valid();
        input.FirstChoice = "Underwater Basket Weaving";

        Assert.True(Validate(input).ContainsKey("Input.FirstChoice"));
    }

    [Fact]
    public async Task BuildAttempts_TagsEachChoiceAndBooksItsSlot()
    {
        using var db = TestSupport.CreateContext();

        var attempts = await ExamChoicePlanner.BuildAttemptsAsync(db, Valid(), "2026-2027");

        Assert.Equal(new int?[] { 1, 2 }, attempts.Select(a => a.ChoiceNumber));
        Assert.Equal(new[] { "Web Development", "Marketing" }, attempts.Select(a => a.Section));
        Assert.Equal(new TimeOnly?[] { new TimeOnly(8, 0), new TimeOnly(9, 30) }, attempts.Select(a => a.ExamTime));
        Assert.All(attempts, a => Assert.Equal(new DateOnly(2026, 11, 3), a.ExamDate));
        Assert.Equal(2, attempts.Select(a => a.Password).Distinct().Count());
    }
}
