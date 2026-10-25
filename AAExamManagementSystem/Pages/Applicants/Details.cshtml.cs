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
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DetailsModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public record AttemptItem(
        int Id,
        string? Section,
        string? DateTaken,
        int TotalScore,
        int MaxScore,
        bool IsSubmitted,
        bool IsApproved,
        int PendingEvaluationCount);

    public Applicant Applicant { get; set; } = null!;

    public IList<AttemptItem> Attempts { get; set; } = new List<AttemptItem>();

    public bool IsAdmin { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        IsAdmin = User.IsInRole(AppRoles.Admin);

        var applicant = await _context.Applicants
            .Include(a => a.Attempts)
            .ThenInclude(at => at.Answers)
            .ThenInclude(ans => ans.Question)
            .FirstOrDefaultAsync(a => a.Id == id && a.IsActive);

        if (applicant is null)
        {
            return NotFound();
        }

        var attempts = applicant.Attempts.AsEnumerable();

        if (!IsAdmin)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var departmentName = currentUser?.SectionId is null
                ? null
                : await _context.Sections
                    .Where(s => s.Id == currentUser.SectionId)
                    .Select(s => s.Name)
                    .FirstOrDefaultAsync();

            // Editors only see applicants who applied to their own department, and only that department's attempts.
            if (departmentName is null || !applicant.Attempts.Any(at => at.Section == departmentName))
            {
                return NotFound();
            }

            attempts = attempts.Where(at => at.Section == departmentName);
        }

        Applicant = applicant;
        Attempts = attempts
            .OrderBy(at => at.Section)
            .Select(at => new AttemptItem(
                at.Id,
                at.Section,
                at.DateTaken,
                at.TotalScore,
                at.Answers.Sum(ans => ans.Question.Score),
                at.DateTaken != null,
                at.IsApproved,
                at.Answers.Count(ans => ans.IsCorrect == null)))
            .ToList();

        return Page();
    }
}
