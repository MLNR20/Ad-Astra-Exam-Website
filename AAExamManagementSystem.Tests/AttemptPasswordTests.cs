using System.Text.RegularExpressions;
using AAExamManagementSystem.Models.Entities;

namespace AAExamManagementSystem.Tests;

public class AttemptPasswordTests
{
    [Theory]
    [InlineData("2026-2027", "AY2627")]
    [InlineData("2027 - 2028", "AY2728")]
    [InlineData("2026", "AY2627")]
    [InlineData("26-27", "AY2627")]
    public void Prefix_ReadsTheAcademicYear(string academicYear, string expected)
    {
        Assert.Equal(expected, AttemptPassword.Prefix(academicYear));
    }

    [Theory]
    [InlineData("", 2026, 10, "AY2627")]
    [InlineData("not a year", 2027, 2, "AY2627")]
    public void Prefix_FallsBackToTheCurrentAcademicYear(string academicYear, int year, int month, string expected)
    {
        Assert.Equal(expected, AttemptPassword.Prefix(academicYear, new DateTime(year, month, 1)));
    }

    [Theory]
    [InlineData("Web Dev", "WEB")]
    [InlineData("Managing", "MAN")]
    [InlineData("  ai", "AIX")]
    [InlineData(null, "XXX")]
    public void ChoiceCode_UsesTheFirstThreeLetters(string? choice, string expected)
    {
        Assert.Equal(expected, AttemptPassword.ChoiceCode(choice));
    }

    [Fact]
    public void Generate_MatchesTheFormat()
    {
        var password = AttemptPassword.Generate("2026-2027", "Web Dev");

        Assert.Matches(new Regex(@"^AY2627WEB\d{4}$"), password);
    }
}
