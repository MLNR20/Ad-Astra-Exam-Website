using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Pages;

/// <summary>
/// The wizard a returning applicant uses to apply again: it refreshes their details on the applicant record
/// and books new exams as attempts under the same account.
/// </summary>
[Authorize(Roles = AppRoles.Applicant)]
public class ReturningModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReturningModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [BindProperty]
    public ReturningApplicantDto Input { get; set; } = new();

    public IList<string> ChoiceOptions { get; private set; } = new List<string>();

    public bool HasApplicantProfile { get; private set; } = true;

    /// <summary>Exams from an earlier application that haven't been taken yet; they have to be finished first.</summary>
    public IList<string> PendingExams { get; private set; } = new List<string>();

    public async Task<IActionResult> OnGetAsync()
    {
        var applicant = await LoadApplicantAsync();
        if (applicant is null)
        {
            HasApplicantProfile = false;
            return Page();
        }

        LoadPendingExams(applicant);
        await LoadChoiceOptionsAsync();

        Input = new ReturningApplicantDto
        {
            StudentNo = applicant.StudentNo,
            FirstName = applicant.FirstName,
            LastName = applicant.LastName,
            Email = applicant.Email,
            YearLevel = applicant.YearLevel,
            School = applicant.School,
            IsScholar = applicant.IsScholar,
            Resume = applicant.Resume,
            ExpectedGradDate = applicant.ExpectedGradDate,
            TermsRemaining = applicant.TermsRemaining,
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var applicant = await LoadApplicantAsync();
        if (applicant is null)
        {
            HasApplicantProfile = false;
            return Page();
        }

        LoadPendingExams(applicant);
        await LoadChoiceOptionsAsync();

        if (PendingExams.Count > 0)
        {
            ModelState.AddModelError(string.Empty, "Finish your pending exams before applying again.");
            return Page();
        }

        ValidateSchool();
        await ValidateUniqueDetailsAsync(applicant);
        ExamChoicePlanner.Validate(ModelState, Input, ChoiceOptions.ToList());
        ValidateChoicesPicked();
        ValidateNotAppliedThisPeriod(applicant);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        applicant.StudentNo = Input.StudentNo;
        applicant.FirstName = Input.FirstName;
        applicant.LastName = Input.LastName;
        applicant.Email = Input.Email;
        applicant.AcademicYear = Input.AcademicYear;
        applicant.YearLevel = Input.YearLevel;
        applicant.School = Input.School;
        applicant.IsScholar = Input.IsScholar;
        applicant.Resume = Input.Resume;
        applicant.ExpectedGradDate = Input.ExpectedGradDate;
        applicant.TermsRemaining = Input.TermsRemaining;
        applicant.DateUpdated = DateTime.UtcNow;

        // The new exams belong to the same applicant, and so to the same user account, as the earlier ones.
        foreach (var attempt in await ExamChoicePlanner.BuildAttemptsAsync(_context, Input, Input.AcademicYear))
        {
            applicant.Attempts.Add(attempt);
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Your application is in. Your new exam passwords and schedule are on your dashboard.";
        return RedirectToPage("/ApplicantPortal");
    }

    private async Task<Applicant?> LoadApplicantAsync()
    {
        var userId = _userManager.GetUserId(User);
        return await _context.Applicants
            .Include(a => a.Attempts)
            .FirstOrDefaultAsync(a => a.UserId == userId);
    }

    private void LoadPendingExams(Applicant applicant)
    {
        PendingExams = applicant.Attempts
            .Where(a => a.DateTaken is null)
            .OrderBy(a => a.Id)
            .Select(a => a.Section ?? "Exam")
            .ToList();
    }

    private async Task LoadChoiceOptionsAsync()
    {
        ChoiceOptions = await _context.Sections
            .AsNoTracking()
            .Where(s => s.Name != AAExamManagementSystem.Models.Entities.Departments.GeneralKnowledge && s.Questions.Any(q => q.IsActive))
            .OrderBy(s => s.Name)
            .Select(s => s.Name)
            .ToListAsync();
    }

    // The school comes from a dropdown, but a posted form can say anything, so check it against the same list.
    private void ValidateSchool()
    {
        var trimmed = Input.School?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            Input.School = null;
            return;
        }

        var match = BenildeSchools.All.FirstOrDefault(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.School)}", "Please pick a school from the list.");
        }

        Input.School = match;
    }

    // Student numbers and emails stay unique across applicants; the applicant's own record doesn't count against them.
    private async Task ValidateUniqueDetailsAsync(Applicant applicant)
    {
        Input.StudentNo = Input.StudentNo?.Trim() ?? string.Empty;
        Input.Email = Input.Email?.Trim() ?? string.Empty;

        var studentNo = Input.StudentNo.ToLower();
        if (studentNo.Length > 0
            && await _context.Applicants.AnyAsync(a => a.Id != applicant.Id && a.StudentNo.Trim().ToLower() == studentNo))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.StudentNo)}", "That student number is already registered.");
        }

        var email = Input.Email.ToLower();
        if (email.Length > 0
            && await _context.Applicants.AnyAsync(a => a.Id != applicant.Id && a.Email.Trim().ToLower() == email))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.Email)}", "That email is already registered to another applicant.");
        }
    }

    private void ValidateChoicesPicked()
    {
        if (string.IsNullOrEmpty(Input.FirstChoice) && string.IsNullOrEmpty(Input.SecondChoice)
            && !ModelState.ContainsKey($"{nameof(Input)}.{nameof(Input.FirstChoice)}"))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.FirstChoice)}", "Pick at least one department to apply to.");
        }
    }

    // An account can apply to a department once per academic year.
    private void ValidateNotAppliedThisPeriod(Applicant applicant)
    {
        var year = Input.AcademicYear?.Trim();
        if (string.IsNullOrEmpty(year))
        {
            return;
        }

        foreach (var (choice, field) in new[] { (Input.FirstChoice, nameof(Input.FirstChoice)), (Input.SecondChoice, nameof(Input.SecondChoice)) })
        {
            if (string.IsNullOrEmpty(choice))
            {
                continue;
            }

            var alreadyApplied = applicant.Attempts.Any(a =>
                string.Equals(a.AcademicYear?.Trim(), year, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.Section, choice, StringComparison.OrdinalIgnoreCase));

            if (alreadyApplied)
            {
                ModelState.AddModelError($"{nameof(Input)}.{field}", $"You've already applied to {choice} for {year}.");
            }
        }
    }
}
