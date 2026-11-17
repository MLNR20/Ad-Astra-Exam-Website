using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AAExamManagementSystem.Pages.Users;

[Authorize(Roles = AppRoles.Admin)]
public class CreateModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IGenericRepository<Section> _sectionRepository;
    private readonly IGenericRepository<Department> _departmentRepository;
    private readonly IGenericRepository<Course> _courseRepository;
    private readonly ActiveIdentityValidator _identity;

    public CreateModel(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IGenericRepository<Section> sectionRepository,
        IGenericRepository<Department> departmentRepository,
        IGenericRepository<Course> courseRepository,
        ActiveIdentityValidator identity)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _sectionRepository = sectionRepository;
        _departmentRepository = departmentRepository;
        _courseRepository = courseRepository;
        _identity = identity;
    }

    [BindProperty]
    public UserCreateDto NewUser { get; set; } = new();

    public SelectList SectionOptions { get; set; } = new(new List<Section>(), "Id", "Name");

    public SelectList DepartmentOptions { get; set; } = new(new List<Department>(), "Id", "Name");

    public SelectList CourseOptions { get; set; } = new(new List<Course>(), "Id", "Name");

    public IList<ApplicationRole> RoleOptions { get; set; } = new List<ApplicationRole>();

    public async Task OnGetAsync()
    {
        await LoadOptionsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync();
            return Page();
        }

        // Only active records hold a username or email; an archived user's can be reused.
        await _identity.ValidateUserNameAsync(ModelState, "NewUser.UserName", NewUser.UserName);
        await _identity.ValidateEmailAsync(ModelState, "NewUser.Email", NewUser.Email);
        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync();
            return Page();
        }

        var activeDepartments = (await _departmentRepository.GetAllAsync()).Select(d => d.Id).ToHashSet();
        if (NewUser.DepartmentId is not null && !activeDepartments.Contains(NewUser.DepartmentId))
        {
            ModelState.AddModelError("NewUser.DepartmentId", "Choose an active department.");
        }

        var activeCourses = (await _courseRepository.GetAllAsync()).Select(c => c.Id).ToHashSet();
        if (NewUser.CourseId is { } courseId && !activeCourses.Contains(courseId))
        {
            ModelState.AddModelError("NewUser.CourseId", "Choose an active course.");
        }

        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync();
            return Page();
        }

        var requestedRoles = NewUser.SelectedRoles is { Count: > 0 } ? NewUser.SelectedRoles : new List<string> { Models.Entities.Roles.Staffer };
        var ruleError = RoleAssignmentRules.Validate(NewUser.UserName, NewUser.SectionId, requestedRoles);
        if (ruleError is not null)
        {
            ModelState.AddModelError(string.Empty, ruleError);
            await LoadOptionsAsync();
            return Page();
        }

        var user = new ApplicationUser
        {
            UserName = NewUser.UserName,
            Email = NewUser.Email,
            EmailConfirmed = true,
            FirstName = NewUser.FirstName,
            LastName = NewUser.LastName,
            SectionId = NewUser.SectionId,
            DepartmentId = NewUser.DepartmentId,
            CourseId = NewUser.CourseId
        };

        var createResult = await _userManager.CreateAsync(user, NewUser.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            await LoadOptionsAsync();
            return Page();
        }

        var selectedRoles = (NewUser.SelectedRoles is { Count: > 0 } ? NewUser.SelectedRoles : new List<string> { Models.Entities.Roles.Staffer });
        var roleResult = await _userManager.AddToRolesAsync(user, selectedRoles);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            foreach (var error in roleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            await LoadOptionsAsync();
            return Page();
        }

        if (!NewUser.IsActive)
        {
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        }

        TempData["SuccessMessage"] = $"User '{user.UserName}' created successfully.";
        return RedirectToPage("Index");
    }

    private async Task LoadOptionsAsync()
    {
        var sections = await _sectionRepository.GetAllAsync();
        SectionOptions = new SelectList(sections.OrderBy(s => s.Name), "Id", "Name");
        DepartmentOptions = new SelectList((await _departmentRepository.GetAllAsync()).OrderBy(d => d.Name), "Id", "Name");
        CourseOptions = new SelectList((await _courseRepository.GetAllAsync()).OrderBy(c => c.Name), "Id", "Name");
        RoleOptions = _roleManager.Roles.Where(r => r.IsActive).OrderBy(r => r.Name).ToList();
    }
}
