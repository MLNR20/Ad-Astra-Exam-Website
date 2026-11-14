using AAExamManagementSystem.Models.Dtos;

namespace AAExamManagementSystem.Services;

/// <summary>
/// The one place that decides who may hold which role. The REST API and the Razor pages both go through it.
/// Assignments are never deleted; deactivating one keeps its history.
/// </summary>
public interface IRoleAssignmentService
{
    Task<List<RoleAssignmentDto>> GetAllAsync();

    /// <summary>Returns the assignment, active or not, or null when the id is malformed or nothing matches.</summary>
    Task<RoleAssignmentDto?> GetByIdAsync(string id);

    /// <summary>Assigns the role, reactivating a deactivated assignment instead of adding a second row.</summary>
    Task<ServiceResult<RoleAssignmentDto>> CreateAsync(RoleAssignmentCreateDto dto);

    /// <summary>Moves the assignment to another role. An admin cannot move themselves off Admin.</summary>
    Task<ServiceResult<RoleAssignmentDto>> ChangeRoleAsync(string id, string newRoleId, string? actingUserId);

    Task<ServiceResult<RoleAssignmentDto>> ActivateAsync(string id);

    /// <summary>Deactivates the assignment. An admin cannot deactivate their own Admin assignment.</summary>
    Task<ServiceResult<RoleAssignmentDto>> DeactivateAsync(string id, string? actingUserId);
}
