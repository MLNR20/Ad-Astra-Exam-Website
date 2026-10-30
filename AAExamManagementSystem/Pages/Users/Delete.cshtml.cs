using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AAExamManagementSystem.Pages.Users;

[Authorize(Roles = AppRoles.Admin)]
public class DeleteModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMapper _mapper;

    public DeleteModel(UserManager<ApplicationUser> userManager, IMapper mapper)
    {
        _userManager = userManager;
        _mapper = mapper;
    }

    public UserDto TargetUser { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null or { IsDeleted: true })
        {
            return NotFound();
        }

        TargetUser = _mapper.Map<UserDto>(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null or { IsDeleted: true })
        {
            return NotFound();
        }

        if (user.Id == _userManager.GetUserId(User))
        {
            TempData["SuccessMessage"] = "You can't delete your own account.";
            return RedirectToPage("Index");
        }

        user.IsDeleted = true;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            await _userManager.UpdateSecurityStampAsync(user);
        }

        TempData["SuccessMessage"] = result.Succeeded
            ? $"User '{user.UserName}' deleted successfully."
            : string.Join(" ", result.Errors.Select(e => e.Description));

        return RedirectToPage("Index");
    }
}
