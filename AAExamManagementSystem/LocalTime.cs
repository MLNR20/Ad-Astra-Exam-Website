using System.Globalization;
using AAExamManagementSystem.Models.Entities;

namespace AAExamManagementSystem;

/// <summary>
/// Shows stored times in the exam time zone (Philippine Standard Time by default). The database keeps UTC, and the
/// host's own time zone is UTC on Render and Azure, so the server's local time can't be used for display.
/// </summary>
public static class LocalTime
{
    public const string DateTimeFormat = "MMM d, yyyy h:mm tt";

    public static DateTime Now => ExamClock.Now;

    /// <summary>A stored UTC time as local time. A value with no kind is read as UTC.</summary>
    public static DateTime Of(DateTime value) => ExamClock.ToLocal(value);

    public static DateTime? Of(DateTime? value) => value is { } v ? ExamClock.ToLocal(v) : null;

    /// <summary>When an exam was taken. It is stored as an ISO 8601 UTC string; anything else is shown as it is.</summary>
    public static string Taken(string? stored, string format = DateTimeFormat) =>
        string.IsNullOrWhiteSpace(stored)
            ? "-"
            : DateTime.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var taken)
                ? ExamClock.ToLocal(taken).ToString(format, CultureInfo.InvariantCulture)
                : stored;
}
