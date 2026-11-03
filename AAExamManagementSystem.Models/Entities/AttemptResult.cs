namespace AAExamManagementSystem.Models.Entities;

public static class AttemptResult
{
    /// <summary>An approved attempt counts as passed when it scores at least this share of the maximum.</summary>
    public const int PassingPercentage = 70;

    public static bool IsPassing(int score, int maxScore) =>
        maxScore > 0 && score * 100 >= PassingPercentage * maxScore;

    /// <summary>True or false once an editor has approved the attempt; null while the result is still open.</summary>
    public static bool? Passed(bool isApproved, int score, int maxScore) =>
        isApproved ? IsPassing(score, maxScore) : null;
}
