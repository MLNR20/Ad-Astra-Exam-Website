using AAExamManagementSystem.Models.Entities;

namespace AAExamManagementSystem.Tests;

public class YearLevelsTests
{
    [Fact]
    public void TheDropdownHasFourOptions_InOrder()
    {
        Assert.Equal(
            new[] { "First Year", "Second Year", "Third Year", "Fourth Year and Beyond" },
            YearLevels.All);
    }

    [Theory]
    [InlineData("First Year", "First Year")]
    [InlineData("second year", "Second Year")]
    [InlineData("  Third Year  ", "Third Year")]
    [InlineData("Fourth Year and Beyond", "Fourth Year and Beyond")]
    public void OptionsMatchIgnoringCaseAndSpaces(string value, string expected)
    {
        Assert.Equal(expected, YearLevels.Normalize(value));
    }

    [Theory]
    [InlineData("1st", "First Year")]
    [InlineData("2nd", "Second Year")]
    [InlineData("3rd Year", "Third Year")]
    [InlineData("3", "Third Year")]
    [InlineData("4th", "Fourth Year and Beyond")]
    [InlineData("5", "Fourth Year and Beyond")]
    [InlineData("fifth year", "Fourth Year and Beyond")]
    public void OlderFreeTextValuesAreMappedOntoAnOption(string value, string expected)
    {
        Assert.Equal(expected, YearLevels.Normalize(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("graduate")]
    [InlineData("0")]
    public void ValuesThatAreNotAYearLevelAreRejected(string? value)
    {
        Assert.Null(YearLevels.Normalize(value));
    }
}
