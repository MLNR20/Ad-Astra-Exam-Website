using System.ComponentModel.DataAnnotations;
using AAExamManagementSystem.Models.Dtos;

namespace AAExamManagementSystem.Tests;

public class NameRulesTests
{
    private static bool IsValid(object dto) => Validator.TryValidateObject(dto, new ValidationContext(dto), new List<ValidationResult>(), true);

    [Theory]
    [InlineData("Art and Design")]
    [InlineData("Photo")]
    [InlineData("Bachelor of Science in Information Technology")]
    public void LettersAndSingleSpaces_AreAccepted(string name)
    {
        Assert.True(IsValid(new SectionCreateUpdateDto { Name = name }));
        Assert.True(IsValid(new DepartmentCreateUpdateDto { Name = name }));
        Assert.True(IsValid(new CourseCreateUpdateDto { Name = name }));
    }

    [Theory]
    [InlineData("Web 2")]
    [InlineData("R&D")]
    [InlineData("Art-Design")]
    [InlineData("Video!")]
    [InlineData("Art  Design")]
    [InlineData(" Photo")]
    [InlineData("Photo ")]
    public void DigitsSymbolsAndStraySpaces_AreRejected(string name)
    {
        Assert.False(IsValid(new SectionCreateUpdateDto { Name = name }));
        Assert.False(IsValid(new DepartmentCreateUpdateDto { Name = name }));
        Assert.False(IsValid(new CourseCreateUpdateDto { Name = name }));
    }

    [Fact]
    public void CourseSchoolName_IsNotRestricted()
    {
        Assert.True(IsValid(new CourseCreateUpdateDto { Name = "Computer Science", SchoolName = "School of Management and Information Technology (SMIT)" }));
    }
}
