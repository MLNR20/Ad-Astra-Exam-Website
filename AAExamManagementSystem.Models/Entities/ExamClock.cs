namespace AAExamManagementSystem.Models.Entities;

/// <summary>
/// "Now" for exam bookings. Slots are booked in local time, but hosts such as Render and Azure run on UTC, so the
/// server's own clock would put "8:00 AM" eight hours off for applicants in the Philippines.
/// </summary>
public static class ExamClock
{
    public const string DefaultTimeZoneId = "Asia/Manila";

    private static TimeZoneInfo _zone = Resolve(DefaultTimeZoneId);

    public static TimeZoneInfo Zone => _zone;

    /// <summary>The current local date and time in the exam time zone.</summary>
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone);

    public static DateOnly Today => DateOnly.FromDateTime(Now);

    /// <summary>Sets the exam time zone from an IANA or Windows id, falling back to Philippine time if it isn't known.</summary>
    public static void Configure(string? timeZoneId) =>
        _zone = Resolve(string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZoneId : timeZoneId.Trim());

    private static TimeZoneInfo Resolve(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // A host without time zone data (a slim container) still gets the right offset for Manila.
            return TimeZoneInfo.CreateCustomTimeZone("PHT", TimeSpan.FromHours(8), "Philippine Time", "Philippine Time");
        }
    }
}
