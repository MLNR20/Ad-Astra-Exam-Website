using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AppRoles = AAExamManagementSystem.Models.Entities.Roles;

namespace AAExamManagementSystem.Pages.Attempts;

[Authorize(Roles = AppRoles.Applicant)]
public class SubmittedModel : PageModel
{
    public const string SectionKey = "ExamSubmittedSection";
    public const string SubmittedAtKey = "ExamSubmittedAt";
    public const string HasNextExamKey = "ExamSubmittedHasNext";

    public string SectionName { get; private set; } = string.Empty;

    public string SubmittedAt { get; private set; } = string.Empty;

    // Where "continue" goes: staff land on the admin dashboard, everyone else on the applicant portal.
    public string ContinuePage =>
        User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Instructor) || User.IsInRole(AppRoles.Staffer)
        || User.IsInRole(AppRoles.Editor) || User.IsInRole(AppRoles.OIC)
            ? "/Dashboard"
            : "/ApplicantPortal";

    // True when the applicant has a second exam still waiting to be taken.
    public bool HasNextExam { get; private set; }

    // Only shown right after submitting an exam; opening it any other way (or refreshing) goes to the portal.
    public IActionResult OnGet()
    {
        if (TempData[SectionKey] is not string section)
        {
            return RedirectToPage("/ApplicantPortal");
        }

        SectionName = section;
        SubmittedAt = TempData[SubmittedAtKey] as string ?? string.Empty;
        HasNextExam = TempData[HasNextExamKey] as bool? ?? false;
        return Page();
    }
}
