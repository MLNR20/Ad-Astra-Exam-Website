using AAExamManagementSystem.Models.Entities;

namespace AAExamManagementSystem.Services;

/// <summary>
/// Editors and OICs only ever see their own department's data, so they can only hold those roles once the user
/// belongs to a department.
/// </summary>
public static class RoleAssignmentRules
{
    public static bool IsDepartmentRole(string? roleName) => roleName is Roles.Editor or Roles.OIC;

    public static string? Validate(string? userName, int? sectionId, IEnumerable<string?> roleNames)
    {
        if (sectionId is not null)
        {
            return null;
        }

        var departmentRole = roleNames.FirstOrDefault(IsDepartmentRole);
        return departmentRole is null
            ? null
            : $"'{userName}' must belong to a department before they can be an {departmentRole}.";
    }
}
