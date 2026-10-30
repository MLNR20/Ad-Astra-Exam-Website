using System.Globalization;
using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Pages
{
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Instructor},{AppRoles.Staffer},{AppRoles.Editor},{AppRoles.OIC}")]
    public class DashboardModel : PageModel
    {
        /// <summary>An approved attempt counts as passed when it scores at least this share of the maximum.</summary>
        public const int PassingPercentage = 70;

        private const int AttentionListSize = 5;
        private const int RecentActivitySize = 6;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public record ApplicantRow(
            int Id,
            string FullName,
            string StudentNo,
            string AcademicYear,
            string Department,
            bool IsSubmitted,
            bool IsApproved,
            int PendingEvaluationCount,
            int TotalScore,
            int MaxScore,
            DateTime DateApplied);

        public record AttentionItem(int AttemptId, string ApplicantName, DateTime? DateTaken, int PendingEvaluationCount, string Department);

        public record ActivityItem(string Description, DateTime When);

        public bool IsAdmin { get; set; }

        /// <summary>Admins see every department; editors and OICs see their own.</summary>
        public bool ShowOverview { get; set; }

        public string? DepartmentName { get; set; }

        public IList<ApplicantRow> Applicants { get; set; } = new List<ApplicantRow>();

        public int TotalApplicants => Applicants.Select(a => a.Id).Distinct().Count();

        public int PassedApplicants { get; set; }

        /// <summary>Submitted attempts, in the department for editors and OICs, that nobody has approved yet.</summary>
        public int AttemptsToCheck { get; set; }

        public IList<AttentionItem> Attention { get; set; } = new List<AttentionItem>();

        public IList<ActivityItem> RecentActivity { get; set; } = new List<ActivityItem>();

        public async Task OnGetAsync()
        {
            IsAdmin = User.IsInRole(AppRoles.Admin);
            var departmentScoped = !IsAdmin && (User.IsInRole(AppRoles.Editor) || User.IsInRole(AppRoles.OIC));
            ShowOverview = IsAdmin || departmentScoped;
            if (!ShowOverview)
            {
                return;
            }

            if (departmentScoped)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.SectionId is null)
                {
                    return;
                }

                DepartmentName = await _context.Sections
                    .Where(s => s.Id == currentUser.SectionId)
                    .Select(s => s.Name)
                    .FirstOrDefaultAsync();

                if (DepartmentName is null)
                {
                    return;
                }
            }

            var query = _context.Applicants
                .Include(a => a.Attempts)
                .ThenInclude(at => at.Answers)
                .ThenInclude(ans => ans.Question)
                .Where(a => a.IsActive);

            if (departmentScoped)
            {
                // An applicant belongs to the editor's department once they hold an exam attempt for it.
                query = query.Where(a => a.Attempts.Any(at => at.Section == DepartmentName));
            }

            var applicants = await query
                .OrderByDescending(a => a.DateCreated)
                .ToListAsync();

            // Editors and OICs get one row per applicant for their department; admins get one per exam attempt.
            var pairs = applicants
                .SelectMany(a => departmentScoped
                    ? new[] { (Applicant: a, Attempt: a.Attempts.First(at => at.Section == DepartmentName)) }
                    : a.Attempts.Select(at => (Applicant: a, Attempt: at)))
                .ToList();

            Applicants = pairs.Select(p => new ApplicantRow(
                p.Applicant.Id,
                FullName(p.Applicant),
                p.Applicant.StudentNo,
                p.Applicant.AcademicYear,
                p.Attempt.Section ?? "-",
                p.Attempt.DateTaken != null,
                p.Attempt.IsApproved,
                p.Attempt.Answers.Count(ans => ans.IsCorrect == null),
                p.Attempt.TotalScore,
                p.Attempt.Answers.Sum(ans => ans.Question.Score),
                p.Applicant.DateCreated)).ToList();

            PassedApplicants = Applicants
                .Where(a => a.IsApproved && a.MaxScore > 0 && a.TotalScore * 100 >= PassingPercentage * a.MaxScore)
                .Select(a => a.Id)
                .Distinct()
                .Count();

            var toCheck = pairs
                .Where(p => p.Attempt.DateTaken != null && !p.Attempt.IsApproved)
                .Select(p => new AttentionItem(
                    p.Attempt.Id,
                    FullName(p.Applicant),
                    ParseTaken(p.Attempt.DateTaken),
                    p.Attempt.Answers.Count(ans => ans.IsCorrect == null),
                    p.Attempt.Section ?? "-"))
                .OrderBy(i => i.DateTaken ?? DateTime.MaxValue)
                .ToList();

            AttemptsToCheck = toCheck.Count;
            Attention = toCheck.Take(AttentionListSize).ToList();

            var activity = new List<ActivityItem>();
            foreach (var (applicant, attempt) in pairs)
            {
                var name = FullName(applicant);

                if (ParseTaken(attempt.DateTaken) is { } taken)
                {
                    var exam = IsAdmin && attempt.Section is not null ? $"the {attempt.Section} exam" : "the exam";
                    activity.Add(new ActivityItem($"{name} submitted {exam}", taken));
                }

                if (attempt.IsApproved)
                {
                    var by = string.IsNullOrWhiteSpace(attempt.ApprovedBy) ? "An editor" : attempt.ApprovedBy;
                    activity.Add(new ActivityItem($"{by} approved {name}'s attempt", DateTime.SpecifyKind(attempt.DateUpdated, DateTimeKind.Utc)));
                }
            }

            RecentActivity = activity
                .OrderByDescending(i => i.When)
                .Take(RecentActivitySize)
                .ToList();
        }

        private static string FullName(Applicant applicant) => $"{applicant.FirstName} {applicant.LastName}".Trim();

        // Attempt.DateTaken is stored as an ISO-8601 string.
        private static DateTime? ParseTaken(string? value) =>
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : null;
    }
}
