using System.Text.RegularExpressions;
using AAExamManagementSystem.Models.Entities;

namespace AAExamManagementSystem.Tests;

public class AttemptPasswordTests
{
    [Theory]
    [InlineData("2026-2027", "AY2728")]
    [InlineData("2027 - 2028", "AY2829")]
    [InlineData("2028-2029", "AY2930")]
    [InlineData("2099-2100", "AY0001")]
    [InlineData("2026", "AY2728")]
    [InlineData("26-27", "AY2728")]
    public void Prefix_IsTheAcademicYearAfterTheTypedOne(string academicYear, string expected)
    {
        Assert.Equal(expected, AttemptPassword.Prefix(academicYear));
    }

    [Theory]
    [InlineData("", 2026, 10, "AY2728")]
    [InlineData("not a year", 2027, 2, "AY2728")]
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

        Assert.Matches(new Regex(@"^AY2728WEB\d{4}$"), password);
    }
}
