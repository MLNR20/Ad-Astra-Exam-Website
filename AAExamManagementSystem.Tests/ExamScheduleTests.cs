using AAExamManagementSystem.Models.Entities;

namespace AAExamManagementSystem.Tests;

public class ExamScheduleTests
{
    [Fact]
    public void Slots_RunBackToBackInNinetyMinutesFromEightAmToEightPm()
    {
        var expected = new[] { "08:00", "09:30", "11:00", "12:30", "14:00", "15:30", "17:00", "18:30" };

        Assert.Equal(expected, ExamSchedule.Slots.Select(s => s.ToString("HH:mm")));
    }

    [Theory]
    [InlineData(7, 30, false)]
    [InlineData(8, 0, true)]
    [InlineData(9, 0, false)]
    [InlineData(18, 30, true)]
    [InlineData(20, 0, false)]
    public void IsValidSlot_OnlyAcceptsSlotStarts(int hour, int minute, bool expected)
    {
        Assert.Equal(expected, ExamSchedule.IsValidSlot(new TimeOnly(hour, minute)));
    }

    [Fact]
    public void SetSchedule_RejectsATimeOutsideTheSlots()
    {
        var attempt = new Attempt();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            attempt.SetSchedule(new DateOnly(2026, 11, 2), new TimeOnly(9, 0)));
        Assert.Null(attempt.ExamDate);
        Assert.Null(attempt.ExamTime);
    }

    [Fact]
    public void IsOpenAt_IsClosedUntilTheBookedSlotStarts()
    {
        var attempt = new Attempt();
        attempt.SetSchedule(new DateOnly(2026, 11, 2), new TimeOnly(9, 30));

        Assert.False(attempt.IsOpenAt(new DateTime(2026, 11, 2, 9, 29, 0)));
        Assert.False(attempt.IsOpenAt(new DateTime(2026, 11, 1, 12, 0, 0)));
        Assert.True(attempt.IsOpenAt(new DateTime(2026, 11, 2, 9, 30, 0)));
        Assert.True(attempt.IsOpenAt(new DateTime(2026, 11, 3, 8, 0, 0)));
    }

    [Fact]
    public void IsOpenAt_IsOpenWhenNoScheduleIsSet()
    {
        Assert.True(new Attempt().IsOpenAt(DateTime.Now));
    }
}
