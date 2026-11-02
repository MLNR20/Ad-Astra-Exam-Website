using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Validation;
using AAExamManagementSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AAExamManagementSystem.Controllers;

/// <summary>Answers the browser's live "is this name free?" check for the department, section and course forms.</summary>
[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("validation")]
public class NameValidationController : ControllerBase
{
    private readonly ActiveNameValidator _validator;

    public NameValidationController(ActiveNameValidator validator)
    {
        _validator = validator;
    }

    // Returns a bare true/false, which is what jQuery Validate's remote rule expects. An inactive record may keep
    // any name, so the check passes when the form is saving the record as inactive.
    [HttpGet("unique-name")]
    public async Task<ActionResult<bool>> UniqueName(NamedEntityKind kind, string? name, string? excludeId, bool active = true)
    {
        return Ok(!active || await _validator.IsAvailableAsync(kind, name, excludeId));
    }
}
