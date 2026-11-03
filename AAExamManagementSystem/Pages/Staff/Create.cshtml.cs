using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AAExamManagementSystem.Pages.Staff;

/// <summary>
/// Lets an editor add a staffer to their own department. The department and the Staffer role are fixed here, so an
/// editor can't use this page to create admins or place someone in another department.
/// </summary>
[Authorize(Roles = AppRoles.Editor)]
public class CreateModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IGenericRepository<Section> _sectionRepository;

    public CreateModel(UserManager<ApplicationUser> userManager, IGenericRepository<Section> sectionRepository)
    {
        _userManager = userManager;
        _sectionRepository = sectionRepository;
    }

    [BindProperty]
    public UserCreateDto NewUser { get; set; } = new();

    public string? DepartmentName { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var editor = await _userManager.GetUserAsync(User);
        if (editor?.SectionId is null)
        {
            return RedirectToPage("Index");
        }

        await LoadDepartmentAsync(editor.SectionId.Value);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var editor = await _userManager.GetUserAsync(User);
        if (editor?.SectionId is null)
        {
            return RedirectToPage("Index");
        }

        await LoadDepartmentAsync(editor.SectionId.Value);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var existingUser = await _userManager.FindByNameAsync(NewUser.UserName)
            ?? await _userManager.FindByEmailAsync(NewUser.Email);
        if (existingUser is not null)
        {
            ModelState.AddModelError(string.Empty, "A user with that username or email already exists.");
            return Page();
        }

        var user = new ApplicationUser
        {
            UserName = NewUser.UserName,
            Email = NewUser.Email,
            EmailConfirmed = true,
            FirstName = NewUser.FirstName,
            LastName = NewUser.LastName,
            SectionId = editor.SectionId
        };

        var createResult = await _userManager.CreateAsync(user, NewUser.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        var roleResult = await _userManager.AddToRoleAsync(user, AppRoles.Staffer);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            foreach (var error in roleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        TempData["SuccessMessage"] = $"Staffer '{user.UserName}' added to your department.";
        return RedirectToPage("Index");
    }

    private async Task LoadDepartmentAsync(int sectionId)
    {
        var sections = await _sectionRepository.GetAllAsync();
        DepartmentName = sections.FirstOrDefault(s => s.Id == sectionId)?.Name;
    }
}
