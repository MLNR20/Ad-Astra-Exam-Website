using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using DepartmentNames = AAExamManagementSystem.Models.Entities.Departments;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Pages.Analytics;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Instructor + "," + AppRoles.Staffer + "," + AppRoles.Editor + "," + AppRoles.OIC)]
public class IndexModel : PageModel
{
    private const int MonthsInTrend = 12;
    private const int MaxTrendMonths = 36;
    private const int ScoreBucketCount = 10;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public int TotalApplicants { get; private set; }
    public int TotalAttempts { get; private set; }
    public double? AverageScore { get; private set; }
    public int TotalQuestions { get; private set; }

    public bool IsAdmin { get; private set; }
    public string? CurrentUserSectionName { get; private set; }

    public IList<LabeledCount> ApplicantsPerMonth { get; private set; } = new List<LabeledCount>();
    public IList<ChoiceCount> DepartmentChoices { get; private set; } = new List<ChoiceCount>();
    public IList<LabeledCount> QuestionsPerSection { get; private set; } = new List<LabeledCount>();
    public IList<LabeledCount> QuestionsPerType { get; private set; } = new List<LabeledCount>();
    public IList<LabeledCount> ScoreDistribution { get; private set; } = new List<LabeledCount>();
    public IList<LabeledCount> ApplicantsPerYearLevel { get; private set; } = new List<LabeledCount>();
    public IList<TopAttempt> TopAttempts { get; private set; } = new List<TopAttempt>();

    public async Task OnGetAsync()
    {
        var scope = await ResolveScopeAsync();

        TotalApplicants = await scope.Applicants.CountAsync();
        TotalAttempts = await scope.Attempts.CountAsync();
        AverageScore = await scope.Attempts.AverageAsync(a => (double?)a.TotalScore);
        TotalQuestions = await scope.Questions.CountAsync();

        await LoadApplicantTrendAsync(scope.Applicants, null, null);
        await LoadDepartmentChoicesAsync(scope.Applicants, scope.ScopedToSection);
        await LoadQuestionBreakdownAsync(IsAdmin, scope.SectionId);
        await LoadScoreDistributionAsync(scope.Attempts);
        await LoadApplicantsPerYearLevelAsync(scope.Applicants);
        await LoadTopAttemptsAsync(scope.Attempts);
    }

    // Each card has its own date range, so a card asks only for its own figures.
    public async Task<IActionResult> OnGetDataAsync(string card, DateOnly? from, DateOnly? to, int tzOffset = 0)
    {
        if (from > to)
        {
            return BadRequest("The start date must be on or before the end date.");
        }

        var scope = InRange(await ResolveScopeAsync(), from, to, Math.Clamp(tzOffset, -840, 840));

        switch (card)
        {
            case "trend":
                await LoadApplicantTrendAsync(scope.Applicants, from, to);
                return new JsonResult(new { rows = ApplicantsPerMonth });
            case "scores":
                await LoadScoreDistributionAsync(scope.Attempts);
                return new JsonResult(new { rows = ScoreDistribution });
            case "departments":
                await LoadDepartmentChoicesAsync(scope.Applicants, scope.ScopedToSection);
                return new JsonResult(new { rows = DepartmentChoices });
            case "yearLevels":
                await LoadApplicantsPerYearLevelAsync(scope.Applicants);
                return new JsonResult(new { rows = ApplicantsPerYearLevel });
            case "top":
                await LoadTopAttemptsAsync(scope.Attempts);
                return new JsonResult(new
                {
                    rows = TopAttempts.Select(t => new
                    {
                        t.ApplicantName,
                        t.StudentNo,
                        t.FirstChoice,
                        t.Score,
                        t.Status,
                        t.StatusBadgeClass,
                        t.DateLabel
                    })
                });
            default:
                return BadRequest("Unknown card.");
        }
    }

    private sealed record Scope(
        IQueryable<Applicant> Applicants,
        IQueryable<Attempt> Attempts,
        IQueryable<Question> Questions,
        bool ScopedToSection,
        int? SectionId);

    // Admins see everything; everyone else is limited to the department (section) their account belongs to.
    private async Task<Scope> ResolveScopeAsync()
    {
        IsAdmin = User.IsInRole(AppRoles.Admin);
        int? currentUserSectionId = null;

        if (!IsAdmin)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            currentUserSectionId = currentUser?.SectionId;
            if (currentUserSectionId is not null)
            {
                CurrentUserSectionName = await _db.Sections.AsNoTracking()
                    .Where(s => s.Id == currentUserSectionId)
                    .Select(s => s.Name)
                    .FirstOrDefaultAsync();
            }
        }

        var scopedToSection = !IsAdmin && CurrentUserSectionName is not null;
        var noSectionAssigned = !IsAdmin && CurrentUserSectionName is null;

        var applicants = _db.Applicants.AsNoTracking().AsQueryable();
        var attempts = _db.Attempts.AsNoTracking().Where(a => a.IsActive && a.DateTaken != null);
        var questions = _db.Questions.AsNoTracking().Where(q => q.IsActive);

        if (noSectionAssigned)
        {
            applicants = applicants.Where(a => false);
            attempts = attempts.Where(a => false);
            questions = questions.Where(q => false);
        }
        else if (scopedToSection)
        {
            applicants = applicants.Where(a => a.Attempts.Any(at => at.Section == CurrentUserSectionName));
            attempts = attempts.Where(a => a.Section == CurrentUserSectionName);
            questions = questions.Where(q => q.SectionId == currentUserSectionId);
        }

        return new Scope(applicants, attempts, questions, scopedToSection, currentUserSectionId);
    }

    // The range is whole days in the viewer's time zone; tzOffset is Date.getTimezoneOffset() (UTC minus local, in minutes).
    // It narrows applicants by when they registered and attempts by when they were created.
    private static Scope InRange(Scope scope, DateOnly? from, DateOnly? to, int tzOffset)
    {
        var applicants = scope.Applicants;
        var attempts = scope.Attempts;

        if (from?.ToDateTime(TimeOnly.MinValue).AddMinutes(tzOffset) is { } rangeStart)
        {
            applicants = applicants.Where(a => a.DateCreated >= rangeStart);
            attempts = attempts.Where(a => a.DateCreated >= rangeStart);
        }

        if (to?.AddDays(1).ToDateTime(TimeOnly.MinValue).AddMinutes(tzOffset) is { } rangeEnd)
        {
            applicants = applicants.Where(a => a.DateCreated < rangeEnd);
            attempts = attempts.Where(a => a.DateCreated < rangeEnd);
        }

        return scope with { Applicants = applicants, Attempts = attempts };
    }

    private async Task LoadTopAttemptsAsync(IQueryable<Attempt> attempts)
    {
        // `attempts` is already limited to the editor's department, so the table only ranks their own applicants.
        var topAttempts = await attempts
            .OrderByDescending(a => a.TotalScore)
            .ThenBy(a => a.DateCreated)
            .Take(5)
            .Select(a => new
            {
                Name = a.Applicant.FirstName + " " + a.Applicant.LastName,
                a.Applicant.StudentNo,
                a.Section,
                a.TotalScore,
                a.DateCreated,
                a.IsApproved,
                MaxScore = a.Answers.Sum(ans => ans.Question.Score),
                PendingEvaluation = a.Answers.Count(ans => ans.IsCorrect == null)
            })
            .ToListAsync();

        TopAttempts = topAttempts
            .Select(a => new TopAttempt(
                a.Name,
                a.StudentNo,
                a.Section,
                a.TotalScore,
                a.DateCreated,
                StatusFor(a.IsApproved, a.TotalScore, a.MaxScore, a.PendingEvaluation)))
            .ToList();
    }

    // Same rule as the dashboard: only an approved attempt can pass, and it needs the passing share of the maximum score.
    private static string StatusFor(bool isApproved, int score, int maxScore, int pendingEvaluation)
    {
        if (!isApproved)
        {
            return pendingEvaluation > 0 ? TopAttemptStatus.NeedsEvaluation : TopAttemptStatus.PendingApproval;
        }

        return maxScore > 0 && score * 100 >= DashboardModel.PassingPercentage * maxScore
            ? TopAttemptStatus.Passed
            : TopAttemptStatus.Failed;
    }

    private async Task LoadApplicantTrendAsync(IQueryable<Applicant> applicants, DateOnly? from, DateOnly? to)
    {
        // Last 12 months by default; a chosen range stretches or shrinks the window to cover it.
        var latest = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var lastMonth = new DateTime(latest.Year, latest.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var firstMonth = from is { } f
            ? new DateTime(f.Year, f.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            : lastMonth.AddMonths(-(MonthsInTrend - 1));
        var monthCount = Math.Clamp((lastMonth.Year - firstMonth.Year) * 12 + lastMonth.Month - firstMonth.Month + 1, 1, MaxTrendMonths);
        var start = lastMonth.AddMonths(-(monthCount - 1));
        var end = start.AddMonths(monthCount);

        var counts = await applicants
            .Where(a => a.DateCreated >= start && a.DateCreated < end)
            .GroupBy(a => new { a.DateCreated.Year, a.DateCreated.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync();

        ApplicantsPerMonth = Enumerable.Range(0, monthCount)
            .Select(i => start.AddMonths(i))
            .Select(m => new LabeledCount(
                m.ToString("MMM yyyy"),
                counts.FirstOrDefault(c => c.Year == m.Year && c.Month == m.Month)?.Count ?? 0))
            .ToList();
    }

    private async Task LoadDepartmentChoicesAsync(IQueryable<Applicant> applicants, bool scopedToSection)
    {
        var picks = applicants.SelectMany(a => a.Attempts).Where(at => at.Section != null && at.Section != "");
        if (scopedToSection)
        {
            // An applicant in the editor's department may also have attempts elsewhere; those aren't theirs to see.
            picks = picks.Where(at => at.Section == CurrentUserSectionName);
        }

        var first = await picks
            .Where(at => at.ChoiceNumber == 1)
            .GroupBy(at => at.Section!)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Name, x => x.Count);

        var second = await picks
            .Where(at => at.ChoiceNumber == 2)
            .GroupBy(at => at.Section!)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Name, x => x.Count);

        var departmentNames = scopedToSection && CurrentUserSectionName is not null
            ? new[] { CurrentUserSectionName }
            : DepartmentNames.All;

        DepartmentChoices = departmentNames
            .Union(first.Keys)
            .Union(second.Keys)
            .Select(name => new ChoiceCount(name, first.GetValueOrDefault(name), second.GetValueOrDefault(name)))
            .OrderByDescending(c => c.FirstChoice + c.SecondChoice)
            .ThenBy(c => c.Name)
            .ToList();
    }

    private async Task LoadQuestionBreakdownAsync(bool isAdmin, int? currentUserSectionId)
    {
        // EF can't translate ordering on a constructor-projected record, so sort an anonymous projection in SQL
        // and build LabeledCount in memory.
        var sectionsQuery = _db.Sections.AsNoTracking().Where(s => s.IsActive);
        if (!isAdmin)
        {
            sectionsQuery = sectionsQuery.Where(s => s.Id == currentUserSectionId);
        }

        var perSection = await sectionsQuery
            .Select(s => new { s.Name, Count = s.Questions.Count(q => q.IsActive) })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        QuestionsPerSection = perSection.Select(x => new LabeledCount(x.Name, x.Count)).ToList();

        var perType = await _db.QuestionTypes.AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => new
            {
                t.Name,
                Count = t.Questions.Count(q => q.IsActive && (isAdmin || q.SectionId == currentUserSectionId))
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        QuestionsPerType = perType.Select(x => new LabeledCount(x.Name, x.Count)).ToList();
    }

    private async Task LoadScoreDistributionAsync(IQueryable<Attempt> attempts)
    {
        var scores = await attempts
            .Select(a => a.TotalScore)
            .ToListAsync();

        if (scores.Count == 0)
        {
            ScoreDistribution = new List<LabeledCount>();
            return;
        }

        var max = scores.Max();
        var width = Math.Max(1, (int)Math.Ceiling((max + 1) / (double)ScoreBucketCount));
        var buckets = new List<LabeledCount>();
        for (var low = 0; low <= max; low += width)
        {
            var high = low + width - 1;
            var label = width == 1 ? low.ToString() : $"{low}–{high}";
            buckets.Add(new LabeledCount(label, scores.Count(s => s >= low && s <= high)));
        }

        ScoreDistribution = buckets;
    }

    private async Task LoadApplicantsPerYearLevelAsync(IQueryable<Applicant> applicants)
    {
        var raw = await applicants
            .GroupBy(a => a.YearLevel)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .ToListAsync();

        // YearLevel is free text ("3rd Year", "2nd", "3"), so bucket by the first digit in it.
        var byYear = new SortedDictionary<int, int>();
        var unspecified = 0;
        foreach (var row in raw)
        {
            var digit = (row.Value ?? string.Empty).FirstOrDefault(char.IsDigit);
            if (digit is >= '1' and <= '9')
            {
                var year = digit - '0';
                byYear[year] = byYear.GetValueOrDefault(year) + row.Count;
            }
            else
            {
                unspecified += row.Count;
            }
        }

        for (var year = 1; year <= 4; year++)
        {
            byYear.TryAdd(year, 0);
        }

        var levels = byYear.Select(x => new LabeledCount($"{Ordinal(x.Key)} Year", x.Value)).ToList();
        if (unspecified > 0)
        {
            levels.Add(new LabeledCount("Not specified", unspecified));
        }
        ApplicantsPerYearLevel = levels;
    }

    private static string Ordinal(int n) => n switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => $"{n}th"
    };

    public record CardFilter(string Card, string Hint);

    public record LabeledCount(string Label, int Count);

    public record ChoiceCount(string Name, int FirstChoice, int SecondChoice);

    public record TopAttempt(string ApplicantName, string StudentNo, string? FirstChoice, int Score, DateTime DateTaken, string Status)
    {
        public string DateLabel => DateTaken.ToLocalTime().ToString("MMM d, yyyy");

        public string StatusBadgeClass => TopAttemptStatus.BadgeClass(Status);
    }

    public static class TopAttemptStatus
    {
        public const string Passed = "Passed";
        public const string Failed = "Failed";
        public const string NeedsEvaluation = "Needs evaluation";
        public const string PendingApproval = "Pending approval";

        public static string BadgeClass(string status) => status switch
        {
            Passed => "text-bg-success",
            Failed => "text-bg-danger",
            NeedsEvaluation => "text-bg-warning",
            _ => "text-bg-secondary"
        };
    }
}
