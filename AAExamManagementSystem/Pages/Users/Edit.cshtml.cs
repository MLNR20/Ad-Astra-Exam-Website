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
public class EditModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IGenericRepository<Section> _sectionRepository;
    private readonly IGenericRepository<Department> _departmentRepository;
    private readonly IGenericRepository<Course> _courseRepository;
    private readonly ActiveIdentityValidator _identity;

    public EditModel(
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

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = string.Empty;

    [BindProperty]
    public UserEditDto TargetUser { get; set; } = new();

    public SelectList SectionOptions { get; set; } = new(new List<Section>(), "Id", "Name");

    public SelectList DepartmentOptions { get; set; } = new(new List<Department>(), "Id", "Name");

    public SelectList CourseOptions { get; set; } = new(new List<Course>(), "Id", "Name");

    public IList<ApplicationRole> RoleOptions { get; set; } = new List<ApplicationRole>();

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.FindByIdAsync(Id);
        if (user is null or { IsDeleted: true })
        {
            return NotFound();
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        TargetUser = new UserEditDto
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            IsActive = !(user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow),
            SectionId = user.SectionId,
            DepartmentId = user.DepartmentId,
            CourseId = user.CourseId,
            SelectedRoles = currentRoles.ToList()
        };
        await LoadOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync();
            return Page();
        }

        var user = await _userManager.FindByIdAsync(Id);
        if (user is null or { IsDeleted: true })
        {
            return NotFound();
        }

        // Only an active department or course can be newly chosen; the one the user already has may stay.
        var activeDepartments = (await _departmentRepository.GetAllAsync()).Select(d => d.Id).ToHashSet();
        if (TargetUser.DepartmentId is not null && TargetUser.DepartmentId != user.DepartmentId && !activeDepartments.Contains(TargetUser.DepartmentId))
        {
            ModelState.AddModelError("TargetUser.DepartmentId", "Choose an active department.");
        }

        var activeCourses = (await _courseRepository.GetAllAsync()).Select(c => c.Id).ToHashSet();
        if (TargetUser.CourseId is { } courseId && courseId != user.CourseId && !activeCourses.Contains(courseId))
        {
            ModelState.AddModelError("TargetUser.CourseId", "Choose an active course.");
        }

        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync();
            return Page();
        }

        if (!string.Equals(user.Email, TargetUser.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (!await _identity.ValidateEmailAsync(ModelState, "TargetUser.Email", TargetUser.Email, excludeUserId: user.Id))
            {
                await LoadOptionsAsync();
                return Page();
            }

            await _userManager.SetEmailAsync(user, TargetUser.Email);
        }

        var ruleError = RoleAssignmentRules.Validate(user.UserName, TargetUser.SectionId, TargetUser.SelectedRoles ?? new List<string>());
        if (ruleError is not null)
        {
            ModelState.AddModelError(string.Empty, ruleError);
            await LoadOptionsAsync();
            return Page();
        }

        user.FirstName = TargetUser.FirstName;
        user.LastName = TargetUser.LastName;
        user.SectionId = TargetUser.SectionId;
        user.DepartmentId = TargetUser.DepartmentId;
        user.CourseId = TargetUser.CourseId;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            await LoadOptionsAsync();
            return Page();
        }

        await _userManager.SetLockoutEndDateAsync(user, TargetUser.IsActive ? null : DateTimeOffset.MaxValue);

        var currentRoles = await _userManager.GetRolesAsync(user);
        var selectedRoles = TargetUser.SelectedRoles ?? new List<string>();
        var rolesToRemove = currentRoles.Except(selectedRoles).ToList();
        var rolesToAdd = selectedRoles.Except(currentRoles).ToList();

        if (rolesToRemove.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                foreach (var error in removeResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                await LoadOptionsAsync();
                return Page();
            }
        }

        if (rolesToAdd.Count > 0)
        {
            var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
            {
                foreach (var error in addResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                await LoadOptionsAsync();
                return Page();
            }
        }

        TempData["SuccessMessage"] = $"User '{user.UserName}' updated successfully.";
        return RedirectToPage("Index");
    }

    private async Task LoadOptionsAsync()
    {
        // Active sections only, plus the one this user already belongs to so saving doesn't silently unassign it.
        var sections = (await _sectionRepository.GetAllIncludingInactiveAsync())
            .Where(s => s.IsActive || s.Id == TargetUser.SectionId);
        SectionOptions = new SelectList(sections.OrderBy(s => s.Name), "Id", "Name");

        var departments = (await _departmentRepository.GetAllIncludingInactiveAsync())
            .Where(d => d.IsActive || d.Id == TargetUser.DepartmentId);
        DepartmentOptions = new SelectList(departments.OrderBy(d => d.Name), "Id", "Name");

        var courses = (await _courseRepository.GetAllIncludingInactiveAsync())
            .Where(c => c.IsActive || c.Id == TargetUser.CourseId);
        CourseOptions = new SelectList(courses.OrderBy(c => c.Name), "Id", "Name");
        RoleOptions = _roleManager.Roles.Where(r => r.IsActive).OrderBy(r => r.Name).ToList();
    }
}
