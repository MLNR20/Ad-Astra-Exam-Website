using Microsoft.AspNetCore.Mvc;

namespace AAExamManagementSystem.ViewComponents;

/// <summary>The applicant portal's top navigation: Dashboard, Attempts and Details, with the current section marked.</summary>
public class PortalNavViewComponent : ViewComponent
{
    public record NavItem(string Page, string Label, string Icon, bool IsActive);

    public IViewComponentResult Invoke()
    {
        var currentPage = ViewContext.RouteData.Values["page"] as string ?? string.Empty;

        // The exam-history pages count as Attempts, and the returning wizard belongs under Details.
        var items = new[]
        {
            Item("/ApplicantPortal", "Dashboard", "bi-grid-1x2", currentPage, "/ApplicantPortal"),
            Item("/Attempts/MyAttempts", "Attempts", "bi-clipboard-check", currentPage, "/Attempts/My"),
            Item("/ApplicantDetails", "Details", "bi-person-vcard", currentPage, "/ApplicantDetails", "/Returning"),
        };

        return View(items);
    }

    private static NavItem Item(string page, string label, string icon, string currentPage, params string[] activeFor) =>
        new(page, label, icon, activeFor.Any(p => currentPage.StartsWith(p, StringComparison.OrdinalIgnoreCase)));
}
