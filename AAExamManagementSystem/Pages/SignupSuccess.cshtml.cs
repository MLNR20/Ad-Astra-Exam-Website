using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AAExamManagementSystem.Pages;

public class SignupSuccessModel : PageModel
{
    public const string SubmittedNameKey = "SignupSubmittedName";
    public const string SubmittedEmailKey = "SignupSubmittedEmail";
    public const string SubmittedAtKey = "SignupSubmittedAt";

    public string ApplicantName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string SubmittedAt { get; private set; } = string.Empty;

    // Only shown right after a registration; opening it any other way goes to the applicant's portal.
    public IActionResult OnGet()
    {
        if (TempData[SubmittedNameKey] is not string name)
        {
            return RedirectToPage("/ApplicantPortal");
        }

        ApplicantName = name;
        Email = TempData[SubmittedEmailKey] as string ?? string.Empty;
        SubmittedAt = TempData[SubmittedAtKey] as string ?? string.Empty;
        return Page();
    }
}
