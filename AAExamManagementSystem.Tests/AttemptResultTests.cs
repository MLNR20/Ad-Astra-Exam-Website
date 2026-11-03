using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;

namespace AAExamManagementSystem.Tests;

public class AttemptResultTests
{
    [Theory]
    [InlineData(70, 100, true)]
    [InlineData(100, 100, true)]
    [InlineData(69, 100, false)]
    [InlineData(0, 100, false)]
    [InlineData(7, 10, true)]
    [InlineData(6, 10, false)]
    [InlineData(0, 0, false)]
    public void IsPassing_NeedsAtLeastSeventyPercentOfTheMaximum(int score, int max, bool expected)
    {
        Assert.Equal(expected, AttemptResult.IsPassing(score, max));
    }

    [Fact]
    public void Passed_IsUndecidedUntilTheAttemptIsApproved()
    {
        Assert.Null(AttemptResult.Passed(isApproved: false, score: 100, maxScore: 100));
        Assert.True(AttemptResult.Passed(isApproved: true, score: 80, maxScore: 100));
        Assert.False(AttemptResult.Passed(isApproved: true, score: 40, maxScore: 100));
    }

    [Fact]
    public void AttemptDtos_ReportTheSameResult()
    {
        var item = new AttemptListItemDto { IsApproved = true, TotalScore = 30, MaxScore = 100 };
        var review = new AttemptReviewDto { IsApproved = true, TotalScore = 90, MaxScore = 100 };

        Assert.False(item.Passed);
        Assert.True(review.Passed);
    }
}
