namespace AAExamManagementSystem.Models.Entities;

/// <summary>The year levels an applicant can pick from.</summary>
public static class YearLevels
{
    public const string First = "First Year";
    public const string Second = "Second Year";
    public const string Third = "Third Year";
    public const string FourthAndBeyond = "Fourth Year and Beyond";

    public static IReadOnlyList<string> All { get; } = new[] { First, Second, Third, FourthAndBeyond };

    /// <summary>
    /// Maps a value onto one of <see cref="All"/>, or null when it doesn't match. Year level used to be free text,
    /// so this also reads older values such as "2nd", "3rd Year" and "3", and treats 4 or more as "Fourth Year and Beyond".
    /// </summary>
    public static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        var exact = All.FirstOrDefault(y => string.Equals(y, trimmed, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var lower = trimmed.ToLowerInvariant();
        if (lower.Contains("beyond") || lower.Contains("fifth") || lower.Contains("sixth"))
        {
            return FourthAndBeyond;
        }

        foreach (var (word, level) in new[] { ("first", First), ("second", Second), ("third", Third), ("fourth", FourthAndBeyond) })
        {
            if (lower.Contains(word))
            {
                return level;
            }
        }

        var digit = trimmed.FirstOrDefault(char.IsDigit);
        return digit switch
        {
            '1' => First,
            '2' => Second,
            '3' => Third,
            >= '4' and <= '9' => FourthAndBeyond,
            _ => null,
        };
    }
}
