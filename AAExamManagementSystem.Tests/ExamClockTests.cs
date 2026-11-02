using AAExamManagementSystem;
using AAExamManagementSystem.Models.Entities;

namespace AAExamManagementSystem.Tests;

public class ExamClockTests
{
    [Fact]
    public void Overlaps_IsTrueForTheSameStartAndForPartiallyOverlappingStarts()
    {
        var start = new DateTime(2026, 11, 3, 9, 30, 0);

        Assert.True(ExamSchedule.Overlaps(start, start));
        Assert.True(ExamSchedule.Overlaps(start, start.AddMinutes(45)));
        Assert.True(ExamSchedule.Overlaps(start.AddMinutes(-89), start));
    }

    [Fact]
    public void Overlaps_IsFalseForBackToBackAndFarApartStarts()
    {
        var start = new DateTime(2026, 11, 3, 9, 30, 0);

        Assert.False(ExamSchedule.Overlaps(start, start.AddMinutes(90)));
        Assert.False(ExamSchedule.Overlaps(start, start.AddMinutes(-90)));
        Assert.False(ExamSchedule.Overlaps(start, start.AddDays(1)));
    }

    [Fact]
    public void TheClockRunsOnPhilippineTimeByDefault_NotTheHostsTimeZone()
    {
        ExamClock.Configure(null);

        var expected = DateTime.UtcNow.AddHours(8);
        Assert.InRange((ExamClock.Now - expected).TotalSeconds, -5, 5);
    }

    [Fact]
    public void AnUnknownTimeZoneFallsBackToPhilippineTime()
    {
        ExamClock.Configure("Not/AZone");

        var expected = DateTime.UtcNow.AddHours(8);
        Assert.InRange((ExamClock.Now - expected).TotalSeconds, -5, 5);

        ExamClock.Configure(null);
    }

    [Fact]
    public void ToLocal_ConvertsUtcToPhilippineTime()
    {
        ExamClock.Configure(null);
        var utc = new DateTime(2026, 11, 2, 6, 30, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 11, 2, 14, 30, 0), ExamClock.ToLocal(utc));
    }

    [Fact]
    public void ToLocal_ReadsAValueWithNoKindAsUtc_LikeOneReadBackFromTheDatabase()
    {
        ExamClock.Configure(null);
        var fromDatabase = new DateTime(2026, 11, 2, 18, 0, 0, DateTimeKind.Unspecified);

        Assert.Equal(new DateTime(2026, 11, 3, 2, 0, 0), ExamClock.ToLocal(fromDatabase));
    }

    [Fact]
    public void Taken_ShowsTheStoredUtcTimestampInPhilippineTime()
    {
        ExamClock.Configure(null);

        Assert.Equal("Nov 2, 2026 2:30 PM", LocalTime.Taken("2026-11-02T06:30:00.0000000Z"));
    }

    [Theory]
    [InlineData(null, "-")]
    [InlineData("", "-")]
    [InlineData("not a date", "not a date")]
    public void Taken_FallsBackForMissingOrUnreadableValues(string? stored, string expected)
    {
        Assert.Equal(expected, LocalTime.Taken(stored));
    }
}
