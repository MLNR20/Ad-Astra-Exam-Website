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
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Instructor},{AppRoles.Staffer},{AppRoles.Editor}")]
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
            bool IsSubmitted,
            bool IsApproved,
            int PendingEvaluationCount,
            int TotalScore,
            int MaxScore,
            DateTime DateApplied);

        public record AttentionItem(int AttemptId, string ApplicantName, DateTime? DateTaken, int PendingEvaluationCount);

        public record ActivityItem(string Description, DateTime When);

        public bool IsEditor { get; set; }

        public string? DepartmentName { get; set; }

        public IList<ApplicantRow> Applicants { get; set; } = new List<ApplicantRow>();

        public int TotalApplicants => Applicants.Count;

        public int PassedApplicants { get; set; }

        /// <summary>Submitted attempts in the department that an editor has not approved yet.</summary>
        public int AttemptsToCheck { get; set; }

        public IList<AttentionItem> Attention { get; set; } = new List<AttentionItem>();

        public IList<ActivityItem> RecentActivity { get; set; } = new List<ActivityItem>();

        public async Task OnGetAsync()
        {
            IsEditor = User.IsInRole(AppRoles.Editor);
            if (!IsEditor)
            {
                return;
            }

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

            // An applicant belongs to the editor's department once they hold an exam attempt for it.
            var applicants = await _context.Applicants
                .Include(a => a.Attempts)
                .ThenInclude(at => at.Answers)
                .ThenInclude(ans => ans.Question)
                .Where(a => a.IsActive && a.Attempts.Any(at => at.Section == DepartmentName))
                .OrderByDescending(a => a.DateCreated)
                .ToListAsync();

            var pairs = applicants
                .Select(a => (Applicant: a, Attempt: a.Attempts.First(at => at.Section == DepartmentName)))
                .ToList();

            Applicants = pairs.Select(p => new ApplicantRow(
                p.Applicant.Id,
                FullName(p.Applicant),
                p.Applicant.StudentNo,
                p.Applicant.AcademicYear,
                p.Attempt.DateTaken != null,
                p.Attempt.IsApproved,
                p.Attempt.Answers.Count(ans => ans.IsCorrect == null),
                p.Attempt.TotalScore,
                p.Attempt.Answers.Sum(ans => ans.Question.Score),
                p.Applicant.DateCreated)).ToList();

            PassedApplicants = Applicants.Count(a =>
                a.IsApproved && a.MaxScore > 0 && a.TotalScore * 100 >= PassingPercentage * a.MaxScore);

            var toCheck = pairs
                .Where(p => p.Attempt.DateTaken != null && !p.Attempt.IsApproved)
                .Select(p => new AttentionItem(
                    p.Attempt.Id,
                    FullName(p.Applicant),
                    ParseTaken(p.Attempt.DateTaken),
                    p.Attempt.Answers.Count(ans => ans.IsCorrect == null)))
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
                    activity.Add(new ActivityItem($"{name} submitted the exam", taken));
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
