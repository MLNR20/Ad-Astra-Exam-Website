using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AAExamManagementSystem.Models.Entities;

/// <summary>
/// Builds attempt passwords: "AY" + the last two digits of each academic year + three letters of the
/// choice + four random digits, e.g. AY2627WEB4821 for "2026-2027" and "Web Dev".
/// </summary>
public static class AttemptPassword
{
    private static readonly Regex FourDigitYear = new(@"\d{4}", RegexOptions.Compiled);
    private static readonly Regex TwoDigitPair = new(@"(\d{2})\D+(\d{2})", RegexOptions.Compiled);

    public static string Generate(string? academicYear, string? choice, DateTime? today = null)
    {
        return Prefix(academicYear, today) + ChoiceCode(choice) + RandomDigits();
    }

    public static string Prefix(string? academicYear, DateTime? today = null)
    {
        var text = academicYear ?? string.Empty;

        var years = FourDigitYear.Matches(text);
        if (years.Count >= 2)
        {
            return $"AY{TwoDigits(years[0].Value)}{TwoDigits(years[1].Value)}";
        }

        if (years.Count == 1)
        {
            var start = int.Parse(years[0].Value);
            return $"AY{start % 100:D2}{(start + 1) % 100:D2}";
        }

        var pair = TwoDigitPair.Match(text);
        if (pair.Success)
        {
            return $"AY{pair.Groups[1].Value}{pair.Groups[2].Value}";
        }

        // No usable year typed: assume the academic year that is running now (starts in June).
        var now = today ?? DateTime.UtcNow;
        var startYear = now.Month >= 6 ? now.Year : now.Year - 1;
        return $"AY{startYear % 100:D2}{(startYear + 1) % 100:D2}";
    }

    public static string ChoiceCode(string? choice)
    {
        var code = new StringBuilder(3);
        foreach (var c in choice ?? string.Empty)
        {
            if (char.IsLetter(c))
            {
                code.Append(char.ToUpperInvariant(c));
                if (code.Length == 3)
                {
                    break;
                }
            }
        }

        while (code.Length < 3)
        {
            code.Append('X');
        }

        return code.ToString();
    }

    private static string TwoDigits(string fourDigitYear) => fourDigitYear[2..];

    private static string RandomDigits() => RandomNumberGenerator.GetInt32(0, 10000).ToString("D4");
}
