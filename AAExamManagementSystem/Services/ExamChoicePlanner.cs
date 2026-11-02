using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Services;

/// <summary>
/// Checks the departments and exam slots an applicant picks, and turns them into attempts. Signup and the
/// returning-applicant wizard share it, so both enforce the same rules.
/// </summary>
public static class ExamChoicePlanner
{
    // The choices come from a dropdown, but a posted form can say anything, so check them against the same list.
    public static void Validate(
        ModelStateDictionary modelState,
        ExamChoicesDto input,
        IReadOnlyCollection<string> options,
        string prefix = "Input",
        DateTime? now = null,
        IEnumerable<DateTime>? existingBookings = null)
    {
        input.FirstChoice = MatchOption(modelState, prefix, nameof(input.FirstChoice), input.FirstChoice, options);
        input.SecondChoice = MatchOption(modelState, prefix, nameof(input.SecondChoice), input.SecondChoice, options);

        if (!string.IsNullOrEmpty(input.FirstChoice)
            && string.Equals(input.FirstChoice, input.SecondChoice, StringComparison.OrdinalIgnoreCase))
        {
            modelState.AddModelError($"{prefix}.{nameof(input.SecondChoice)}", "Pick a different department for your second choice.");
        }

        var clock = now ?? ExamClock.Now;
        CheckSlot(modelState, prefix, "First", input.FirstChoice, input.FirstExamDate, input.FirstExamTime, clock);
        CheckSlot(modelState, prefix, "Second", input.SecondChoice, input.SecondExamDate, input.SecondExamTime, clock);

        var firstStart = SlotStart(input.FirstChoice, input.FirstExamDate, input.FirstExamTime);
        var secondStart = SlotStart(input.SecondChoice, input.SecondExamDate, input.SecondExamTime);

        if (firstStart is { } first && secondStart is { } second && ExamSchedule.Overlaps(first, second))
        {
            modelState.AddModelError($"{prefix}.{nameof(input.SecondExamTime)}",
                "Your two exams can't be at the same time. Each one runs 1 hour 30 minutes, so book them in different slots.");
        }

        // Exams the applicant already has booked count too, so a returning applicant can't double-book.
        var booked = existingBookings?.ToList() ?? new List<DateTime>();
        CheckAgainstBookings(modelState, prefix, "First", firstStart, booked);
        CheckAgainstBookings(modelState, prefix, "Second", secondStart, booked);
    }

    // The start of a pick, or null while it's incomplete or not a real slot (those get their own errors).
    private static DateTime? SlotStart(string? choice, DateOnly? date, TimeOnly? time) =>
        !string.IsNullOrEmpty(choice) && date is { } d && time is { } t && ExamSchedule.IsValidSlot(t)
            ? d.ToDateTime(t)
            : null;

    private static void CheckAgainstBookings(
        ModelStateDictionary modelState, string prefix, string which, DateTime? start, IReadOnlyCollection<DateTime> booked)
    {
        if (start is not { } slot)
        {
            return;
        }

        var clash = booked.Where(b => ExamSchedule.Overlaps(slot, b)).Select(b => (DateTime?)b).FirstOrDefault();
        if (clash is { } when)
        {
            modelState.AddModelError($"{prefix}.{which}ExamTime",
                $"You already have an exam booked on {when:MMM d, yyyy} at {when:h:mm tt}. Pick a different slot.");
        }
    }

    // One attempt per distinct choice, so a second choice gives the applicant a second exam to take.
    public static async Task<IList<Attempt>> BuildAttemptsAsync(ApplicationDbContext context, ExamChoicesDto input, string academicYear)
    {
        var picks = new[]
        {
            (Number: 1, Choice: input.FirstChoice, Date: input.FirstExamDate, Time: input.FirstExamTime),
            (Number: 2, Choice: input.SecondChoice, Date: input.SecondExamDate, Time: input.SecondExamTime),
        };

        var attempts = new List<Attempt>();
        var usedPasswords = new HashSet<string>();
        var chosen = new List<string>();

        foreach (var pick in picks)
        {
            var trimmed = pick.Choice?.Trim();
            if (string.IsNullOrEmpty(trimmed) || chosen.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            chosen.Add(trimmed);

            var sectionName = await context.Sections
                .Where(s => s.IsActive && s.Name.ToLower() == trimmed.ToLower())
                .Select(s => s.Name)
                .FirstOrDefaultAsync() ?? trimmed;

            var attempt = new Attempt
            {
                AcademicYear = academicYear,
                Section = sectionName,
                ChoiceNumber = pick.Number,
                Password = await GeneratePasswordAsync(context, academicYear, sectionName, usedPasswords),
            };

            if (pick.Date is { } date && pick.Time is { } time)
            {
                attempt.SetSchedule(date, time);
            }

            attempts.Add(attempt);
        }

        return attempts;
    }

    private static string? MatchOption(
        ModelStateDictionary modelState, string prefix, string field, string? choice, IReadOnlyCollection<string> options)
    {
        var trimmed = choice?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        var match = options.FirstOrDefault(o => string.Equals(o, trimmed, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            modelState.AddModelError($"{prefix}.{field}", "Please pick a department from the list.");
        }

        return match;
    }

    // A chosen department needs a date and one of the exam slots, and the slot can't already be over.
    private static void CheckSlot(
        ModelStateDictionary modelState, string prefix, string which, string? choice, DateOnly? date, TimeOnly? time, DateTime now)
    {
        if (string.IsNullOrEmpty(choice))
        {
            return;
        }

        var dateKey = $"{prefix}.{which}ExamDate";
        var timeKey = $"{prefix}.{which}ExamTime";

        if (date is null)
        {
            modelState.AddModelError(dateKey, $"Pick a date for your {which.ToLowerInvariant()} exam.");
        }

        if (time is null)
        {
            modelState.AddModelError(timeKey, $"Pick a time for your {which.ToLowerInvariant()} exam.");
        }
        else if (!ExamSchedule.IsValidSlot(time.Value))
        {
            modelState.AddModelError(timeKey, "Exams run in 1 hour 30 minute slots between 8:00 AM and 8:00 PM.");
        }

        if (date is { } d && time is { } t && ExamSchedule.IsValidSlot(t) && d.ToDateTime(t) < now)
        {
            modelState.AddModelError(dateKey, "Pick a date and time that hasn't passed yet.");
        }
    }

    private static async Task<string> GeneratePasswordAsync(
        ApplicationDbContext context, string academicYear, string sectionName, ISet<string> usedPasswords)
    {
        while (true)
        {
            var password = AttemptPassword.Generate(academicYear, sectionName);
            if (!usedPasswords.Contains(password)
                && !await context.Attempts.AnyAsync(a => a.Password == password))
            {
                usedPasswords.Add(password);
                return password;
            }
        }
    }
}
