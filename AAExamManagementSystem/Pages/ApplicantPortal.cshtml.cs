using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Pages;

[Authorize(Roles = AppRoles.Applicant)]
public class ApplicantPortalModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMapper _mapper;

    public ApplicantPortalModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IMapper mapper)
    {
        _context = context;
        _userManager = userManager;
        _mapper = mapper;
    }

    public ApplicantDto? Applicant { get; set; }
    public int AttemptCount { get; set; }

    /// <summary>How many applications the applicant has made: one per academic year they booked exams for.</summary>
    public int ApplicationCount { get; set; }
    public AttemptListItemDto? LatestAttempt { get; set; }
    public string? FirstChoice { get; set; }
    public string? SecondChoice { get; set; }
    public IList<PendingAttemptItem> PendingAttempts { get; set; } = new List<PendingAttemptItem>();

    public record PendingAttemptItem(string? Section, string? Password, DateTime? ScheduledStart, bool IsOpen);

    // The exam waits for its booked slot; the next pending exam is the one Take serves.
    public DateTime? NextExamOpensAt => PendingAttempts.FirstOrDefault() is { IsOpen: false } next ? next.ScheduledStart : null;

    public async Task OnGetAsync()
    {
        var userId = _userManager.GetUserId(User);
        var applicant = await _context.Applicants
            .Include(a => a.Attempts)
            .ThenInclude(a => a.Answers)
            .ThenInclude(ans => ans.Question)
            .FirstOrDefaultAsync(a => a.UserId == userId);

        if (applicant is null)
        {
            return;
        }

        Applicant = _mapper.Map<ApplicantDto>(applicant);
        FirstChoice = applicant.Attempts.ChoiceName(1);
        SecondChoice = applicant.Attempts.ChoiceName(2);
        // Attempts are created at signup, so only the ones with a date taken have actually been sat.
        var takenAttempts = applicant.Attempts.Where(a => a.DateTaken is not null).ToList();
        AttemptCount = takenAttempts.Count;
        ApplicationCount = Applicant.IsActive
            ? applicant.Attempts.Select(a => a.AcademicYear?.Trim().ToLowerInvariant()).Where(y => !string.IsNullOrEmpty(y)).Distinct().Count()
            : 0;

        var now = ExamClock.Now;
        PendingAttempts = applicant.Attempts
            .Where(a => a.DateTaken is null)
            .OrderBy(a => a.Id)
            .Select(a => new PendingAttemptItem(a.Section, a.Password, a.ScheduledStart, a.IsOpenAt(now)))
            .ToList();

        var attempt = takenAttempts.OrderByDescending(a => a.DateCreated).FirstOrDefault();
        if (attempt is not null)
        {
            LatestAttempt = new AttemptListItemDto
            {
                Id = attempt.Id,
                ApplicantName = $"{applicant.FirstName} {applicant.LastName}",
                StudentNo = applicant.StudentNo,
                Section = attempt.Section,
                DateTaken = attempt.DateTaken,
                TotalScore = attempt.TotalScore,
                MaxScore = attempt.Answers.Sum(ans => ans.Question.Score),
                PendingEvaluationCount = attempt.Answers.Count(ans => ans.IsCorrect == null),
                IsApproved = attempt.IsApproved,
                DateCreated = attempt.DateCreated,
            };
        }
    }
}
