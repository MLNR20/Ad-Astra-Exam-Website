namespace AAExamManagementSystem.Models.Dtos;

/// <summary>Section, course and department names are plain words: letters separated by single spaces.</summary>
public static class NameRules
{
    public const string LettersOnlyPattern = @"^[A-Za-z]+( [A-Za-z]+)*$";

    public const string LettersOnlyMessage = "The {0} can only contain letters and single spaces.";
}
