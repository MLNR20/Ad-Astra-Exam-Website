using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Pages.Applicants;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Editor)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public record ApplicantListItem(
        int Id,
        string FullName,
        string StudentNo,
        string Email,
        string AcademicYear,
        string? YearLevel,
        string? School,
        bool IsScholar,
        string? FirstChoice,
        string? SecondChoice,
        string? ExpectedGradDate,
        int TermsRemaining,
        string? Resume,
        IList<string> AppliedSections,
        DateTime DateApplied);

    public IList<ApplicantListItem> Applicants { get; set; } = new List<ApplicantListItem>();

    public bool IsAdmin { get; set; }

    public string? DepartmentName { get; set; }

    // Archiving hides the applicant and locks their login; their attempts and answers stay on record.
    public async Task<IActionResult> OnPostArchiveAsync(int id)
    {
        if (!User.IsInRole(AppRoles.Admin))
        {
            return Forbid();
        }

        var applicant = await _context.Applicants.FirstOrDefaultAsync(a => a.Id == id && a.IsActive);
        if (applicant is null)
        {
            return NotFound();
        }

        applicant.IsActive = false;
        applicant.DateUpdated = DateTime.UtcNow;

        var user = await _userManager.FindByIdAsync(applicant.UserId);
        if (user is not null)
        {
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;
            await _userManager.UpdateAsync(user);
            await _userManager.UpdateSecurityStampAsync(user);
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Applicant '{applicant.FirstName} {applicant.LastName}'".TrimEnd() + " archived successfully.";
        return RedirectToPage();
    }

    public async Task OnGetAsync()
    {
        IsAdmin = User.IsInRole(AppRoles.Admin);

        var query = _context.Applicants
            .Include(a => a.Attempts)
            .Where(a => a.IsActive)
            .AsQueryable();

        if (!IsAdmin)
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

            // An applicant belongs to a department once they hold an exam attempt for it.
            query = query.Where(a => a.Attempts.Any(at => at.Section == DepartmentName));
        }

        var applicants = await query
            .OrderByDescending(a => a.DateCreated)
            .ToListAsync();

        Applicants = applicants.Select(a => new ApplicantListItem(
            a.Id,
            $"{a.FirstName} {a.LastName}".Trim(),
            a.StudentNo,
            a.Email,
            a.AcademicYear,
            a.YearLevel,
            a.School,
            a.IsScholar,
            a.Attempts.ChoiceName(1),
            a.Attempts.ChoiceName(2),
            a.ExpectedGradDate,
            a.TermsRemaining,
            a.Resume,
            a.Attempts
                .Select(at => at.Section)
                .OfType<string>()
                .Distinct()
                .ToList(),
            a.DateCreated)).ToList();
    }
}
