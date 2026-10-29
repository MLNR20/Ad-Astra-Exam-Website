using AAExamManagementSystem.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AAExamManagementSystem.ViewComponents;

/// <summary>
/// The applicant portal's navbar: the title and welcome line on top, with the section links and a container
/// holding the sign out button and logo along its bottom row.
/// </summary>
public class ApplicantNavbarViewComponent : ViewComponent
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ApplicantNavbarViewComponent(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public record NavItem(string Page, string Label, string Icon, bool IsActive);

    public record NavbarModel(string? UserName, IReadOnlyList<NavItem> Items);

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = UserClaimsPrincipal.Identity?.IsAuthenticated == true
            ? await _userManager.GetUserAsync(UserClaimsPrincipal)
            : null;
        var userName = user is null ? null : $"{user.FirstName} {user.LastName}".Trim();

        var currentPage = ViewContext.RouteData.Values["page"] as string ?? string.Empty;

        // The exam-history pages count as Attempts, and the returning wizard belongs under Details.
        var items = new[]
        {
            Item("/ApplicantPortal", "Dashboard", "bi-grid-1x2", currentPage, "/ApplicantPortal"),
            Item("/Attempts/MyAttempts", "Attempts", "bi-clipboard-check", currentPage, "/Attempts/My"),
            Item("/ApplicantDetails", "Details", "bi-person-vcard", currentPage, "/ApplicantDetails", "/Returning"),
        };

        return View(new NavbarModel(userName, items));
    }

    private static NavItem Item(string page, string label, string icon, string currentPage, params string[] activeFor) =>
        new(page, label, icon, activeFor.Any(p => currentPage.StartsWith(p, StringComparison.OrdinalIgnoreCase)));
}
