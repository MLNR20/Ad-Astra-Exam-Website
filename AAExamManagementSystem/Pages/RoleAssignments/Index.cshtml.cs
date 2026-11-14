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
public class IndexModel : PageModel
{
    private readonly IRoleAssignmentService _roleAssignmentService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public IndexModel(IRoleAssignmentService roleAssignmentService, UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
        _roleAssignmentService = roleAssignmentService;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public IList<RoleAssignmentDto> Assignments { get; set; } = new List<RoleAssignmentDto>();

    [BindProperty]
    public RoleAssignmentCreateDto NewAssignment { get; set; } = new();

    public SelectList UserOptions { get; set; } = new(new List<object>(), "Id", "DisplayName");
    public SelectList RoleOptions { get; set; } = new(new List<ApplicationRole>(), "Id", "Name");

    public bool ShowCreateModal { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAssignmentsAsync();
        LoadOptions();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (ModelState.IsValid)
        {
            var result = await _roleAssignmentService.CreateAsync(NewAssignment);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"Role '{result.Value!.RoleName}' assigned to '{result.Value.UserName}' successfully.";
                return RedirectToPage("Index");
            }

            ModelState.AddModelError(string.Empty, result.Error ?? "The role could not be assigned.");
        }

        await LoadAssignmentsAsync();
        LoadOptions();
        ShowCreateModal = true;
        return Page();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(string id)
    {
        var result = await _roleAssignmentService.DeactivateAsync(id, _userManager.GetUserId(User));
        switch (result.Status)
        {
            case ServiceStatus.NotFound:
                return NotFound();
            case ServiceStatus.Success:
                TempData["SuccessMessage"] = $"Role '{result.Value!.RoleName}' deactivated for '{result.Value.UserName}' successfully.";
                break;
            default:
                TempData["SuccessMessage"] = result.Error;
                break;
        }

        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostActivateAsync(string id)
    {
        var result = await _roleAssignmentService.ActivateAsync(id);
        switch (result.Status)
        {
            case ServiceStatus.NotFound:
                return NotFound();
            case ServiceStatus.Success:
                TempData["SuccessMessage"] = $"Role '{result.Value!.RoleName}' activated for '{result.Value.UserName}' successfully.";
                break;
            default:
                TempData["SuccessMessage"] = result.Error;
                break;
        }

        return RedirectToPage("Index");
    }

    private async Task LoadAssignmentsAsync()
    {
        Assignments = await _roleAssignmentService.GetAllAsync();
    }

    private void LoadOptions()
    {
        var users = _userManager.Users.Where(u => !u.IsDeleted).OrderBy(u => u.UserName).ToList();
        UserOptions = new SelectList(
            users.Select(u => new { u.Id, DisplayName = $"{u.FirstName} {u.LastName} ({u.UserName})".Trim() }),
            "Id", "DisplayName");

        var roles = _roleManager.Roles.Where(r => r.IsActive).OrderBy(r => r.Name).ToList();
        RoleOptions = new SelectList(roles, "Id", "Name");
    }
}
