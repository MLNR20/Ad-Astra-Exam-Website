namespace AAExamManagementSystem.Models.Entities;

/// <summary>
/// The exam time slots: 1 hour 30 minute sittings that run back to back from 8:00 AM to 8:00 PM
/// (8:00, 9:30, 11:00, 12:30, 2:00, 3:30, 5:00 and 6:30 PM).
/// </summary>
public static class ExamSchedule
{
    public static readonly TimeOnly Opens = new(8, 0);
    public static readonly TimeOnly Closes = new(20, 0);
    public static readonly TimeSpan SlotLength = TimeSpan.FromMinutes(90);

    /// <summary>Every start time an exam can be booked at.</summary>
    public static IReadOnlyList<TimeOnly> Slots { get; } = BuildSlots();

    public static bool IsValidSlot(TimeOnly time) => Slots.Contains(time);

    public static string FormatSlot(TimeOnly start) =>
        $"{start:h:mm tt} - {start.Add(SlotLength):h:mm tt}";

    private static IReadOnlyList<TimeOnly> BuildSlots()
    {
        var slots = new List<TimeOnly>();
        for (var start = Opens; start.Add(SlotLength) <= Closes; start = start.Add(SlotLength))
        {
            slots.Add(start);
        }

        return slots;
    }
}
