using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AAExamManagementSystem.Pages.RoleAssignments;

[Authorize(Roles = AppRoles.Admin)]
public class EditModel : PageModel
{
    private readonly IRoleAssignmentService _roleAssignmentService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public EditModel(IRoleAssignmentService roleAssignmentService, UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
        _roleAssignmentService = roleAssignmentService;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;

    [BindProperty]
    public RoleAssignmentEditDto Assignment { get; set; } = new();

    public string UserDisplayName { get; set; } = string.Empty;

    public SelectList RoleOptions { get; set; } = new(new List<ApplicationRole>(), "Id", "Name");

    public async Task<IActionResult> OnGetAsync()
    {
        var current = await _roleAssignmentService.GetByIdAsync(Id);
        if (current is null)
        {
            return NotFound();
        }

        UserDisplayName = await DisplayNameForAsync(current.UserId, current.UserName);
        Assignment = new RoleAssignmentEditDto { RoleId = current.RoleId };
        LoadRoleOptions(current.RoleId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var current = await _roleAssignmentService.GetByIdAsync(Id);
        if (current is null)
        {
            return NotFound();
        }

        UserDisplayName = await DisplayNameForAsync(current.UserId, current.UserName);

        if (!ModelState.IsValid)
        {
            LoadRoleOptions(current.RoleId);
            return Page();
        }

        var result = await _roleAssignmentService.ChangeRoleAsync(Id, Assignment.RoleId, _userManager.GetUserId(User));
        if (result.Status == ServiceStatus.NotFound)
        {
            return NotFound();
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError("Assignment.RoleId", result.Error ?? "The role could not be changed.");
            LoadRoleOptions(current.RoleId);
            return Page();
        }

        TempData["SuccessMessage"] = $"Role assignment for '{result.Value!.UserName}' updated successfully.";
        return RedirectToPage("Index");
    }

    // The edit form shows "First Last (login)", which the assignment itself does not carry.
    private async Task<string> DisplayNameForAsync(string userId, string fallback)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user is null ? fallback : $"{user.FirstName} {user.LastName} ({user.UserName})".Trim();
    }

    private void LoadRoleOptions(string currentRoleId)
    {
        var roles = _roleManager.Roles.Where(r => r.IsActive).OrderBy(r => r.Name).ToList();
        RoleOptions = new SelectList(roles, "Id", "Name", currentRoleId);
    }
}
