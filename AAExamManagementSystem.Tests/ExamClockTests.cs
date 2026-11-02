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
}
