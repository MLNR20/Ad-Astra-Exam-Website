namespace AAExamManagementSystem.Models.Entities;

public class Attempt
{
    public int Id { get; set; }
    public int ApplicantId { get; set; }
    public Applicant Applicant { get; set; } = null!;
    public int TotalScore { get; set; }
    public string? DateTaken { get; set; }
    public DateTime? TimeRemaining { get; set; }
    public int? ExamId { get; set; }
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
    public string? Section { get; set; }
    public string? Password { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public bool IsApproved { get; set; }
    public string? ApprovedBy { get; set; }

    /// <summary>JSON of the answers an applicant has entered so far; cleared once the exam is submitted.</summary>
    public string? ProgressJson { get; set; }

    /// <summary>1 for the applicant's first choice, 2 for their second; null for exams not tied to a choice.</summary>
    public int? ChoiceNumber { get; set; }

    /// <summary>The day the applicant is booked to sit this exam; null while no schedule is set.</summary>
    public DateOnly? ExamDate { get; set; }

    /// <summary>The start of the booked <see cref="ExamSchedule"/> slot on <see cref="ExamDate"/>.</summary>
    public TimeOnly? ExamTime { get; set; }

    public ICollection<Answer> Answers { get; set; } = new List<Answer>();

    /// <summary>Books the exam, rejecting a time that is not one of the <see cref="ExamSchedule.Slots"/>.</summary>
    public void SetSchedule(DateOnly date, TimeOnly time)
    {
        if (!ExamSchedule.IsValidSlot(time))
        {
            throw new ArgumentOutOfRangeException(nameof(time), time,
                "Exams run in 1 hour 30 minute slots between 8:00 AM and 8:00 PM.");
        }

        ExamDate = date;
        ExamTime = time;
    }

    /// <summary>When the booked slot starts, or null when the exam has no schedule.</summary>
    public DateTime? ScheduledStart =>
        ExamDate is { } date && ExamTime is { } time ? date.ToDateTime(time) : null;

    /// <summary>True when the applicant may start: no schedule is set, or the booked time has arrived.</summary>
    public bool IsOpenAt(DateTime now) => ScheduledStart is not { } start || now >= start;
}

public static class AttemptChoices
{
    /// <summary>The department the applicant picked as choice <paramref name="number"/>, taking the most recent application.</summary>
    public static string? ChoiceName(this IEnumerable<Attempt> attempts, int number) =>
        attempts.Where(a => a.ChoiceNumber == number).OrderByDescending(a => a.Id).FirstOrDefault()?.Section;
}
