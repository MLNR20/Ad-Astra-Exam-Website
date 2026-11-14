using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AAExamManagementSystem.Pages.RoleAssignments;

[Authorize(Roles = AppRoles.Admin)]
public class DetailsModel : PageModel
{
    private readonly IRoleAssignmentService _roleAssignmentService;

    public DetailsModel(IRoleAssignmentService roleAssignmentService)
    {
        _roleAssignmentService = roleAssignmentService;
    }

    public RoleAssignmentDto Assignment { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var assignment = await _roleAssignmentService.GetByIdAsync(id);
        if (assignment is null)
        {
            return NotFound();
        }

        Assignment = assignment;
        return Page();
    }
}
