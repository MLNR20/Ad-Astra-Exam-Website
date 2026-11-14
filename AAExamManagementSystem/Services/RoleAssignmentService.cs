using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AppRoles = AAExamManagementSystem.Models.Entities.Roles;

namespace AAExamManagementSystem.Services;

public class RoleAssignmentService : IRoleAssignmentService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ApplicationDbContext _dbContext;

    public RoleAssignmentService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
    }

    public async Task<List<RoleAssignmentDto>> GetAllAsync()
    {
        var rows = await (
            from ur in _dbContext.UserRoles.IgnoreQueryFilters()
            join u in _dbContext.Users on ur.UserId equals u.Id
            join r in _dbContext.Roles on ur.RoleId equals r.Id
            orderby u.UserName, r.Name
            select new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.UserName,
                RoleId = r.Id,
                RoleName = r.Name,
                ur.CreatedAt,
                ur.DateUpdated,
                ur.IsActive
            }).ToListAsync();

        return rows.Select(row => new RoleAssignmentDto
        {
            Id = RoleAssignmentId.Combine(row.Id, row.RoleId),
            UserId = row.Id,
            UserName = DisplayName(row.FirstName, row.LastName, row.UserName),
            RoleId = row.RoleId,
            RoleName = row.RoleName ?? string.Empty,
            IsActive = row.IsActive,
            CreatedAt = row.CreatedAt,
            DateUpdated = row.DateUpdated
        }).ToList();
    }

    public async Task<RoleAssignmentDto?> GetByIdAsync(string id)
    {
        if (!RoleAssignmentId.TryParse(id, out var userId, out var roleId)) return null;

        var user = await _userManager.FindByIdAsync(userId);
        var role = await _roleManager.FindByIdAsync(roleId);
        if (user is null || role is null || string.IsNullOrEmpty(role.Name)) return null;

        return await BuildAsync(user, role);
    }

    public async Task<ServiceResult<RoleAssignmentDto>> CreateAsync(RoleAssignmentCreateDto dto)
    {
        var user = await _userManager.FindByIdAsync(dto.UserId);
        if (user is null) return ServiceResult<RoleAssignmentDto>.Invalid("User not found.");

        var role = await _roleManager.FindByIdAsync(dto.RoleId);
        if (role is null || string.IsNullOrEmpty(role.Name)) return ServiceResult<RoleAssignmentDto>.Invalid("Role not found.");

        var error = await CheckCanHoldAsync(user, role);
        if (error is not null) return ServiceResult<RoleAssignmentDto>.Invalid(error);

        await AddOrReactivateAsync(user.Id, role.Id);
        await _dbContext.SaveChangesAsync();

        return ServiceResult<RoleAssignmentDto>.Success((await BuildAsync(user, role))!);
    }

    public async Task<ServiceResult<RoleAssignmentDto>> ChangeRoleAsync(string id, string newRoleId, string? actingUserId)
    {
        if (!RoleAssignmentId.TryParse(id, out var userId, out var oldRoleId)) return ServiceResult<RoleAssignmentDto>.NotFound();

        var user = await _userManager.FindByIdAsync(userId);
        var oldRole = await _roleManager.FindByIdAsync(oldRoleId);
        if (user is null || oldRole is null || string.IsNullOrEmpty(oldRole.Name)) return ServiceResult<RoleAssignmentDto>.NotFound();

        var newRole = await _roleManager.FindByIdAsync(newRoleId);
        if (newRole is null || string.IsNullOrEmpty(newRole.Name)) return ServiceResult<RoleAssignmentDto>.Invalid("Role not found.");

        if (string.Equals(oldRole.Id, newRole.Id, StringComparison.Ordinal))
        {
            var unchanged = await BuildAsync(user, oldRole);
            return unchanged is null ? ServiceResult<RoleAssignmentDto>.NotFound() : ServiceResult<RoleAssignmentDto>.Success(unchanged);
        }

        var error = await CheckCanHoldAsync(user, newRole);
        if (error is not null) return ServiceResult<RoleAssignmentDto>.Invalid(error);

        if (oldRole.Name == AppRoles.Admin && userId == actingUserId)
            return ServiceResult<RoleAssignmentDto>.Forbidden("You can't move yourself off the Admin role.");

        // One save for both rows, so a failure cannot leave the user without either role.
        await DeactivateRowAsync(userId, oldRoleId);
        await AddOrReactivateAsync(userId, newRole.Id);
        await _dbContext.SaveChangesAsync();

        return ServiceResult<RoleAssignmentDto>.Success((await BuildAsync(user, newRole))!);
    }

    public async Task<ServiceResult<RoleAssignmentDto>> ActivateAsync(string id)
    {
        if (!RoleAssignmentId.TryParse(id, out var userId, out var roleId)) return ServiceResult<RoleAssignmentDto>.NotFound();

        var user = await _userManager.FindByIdAsync(userId);
        var role = await _roleManager.FindByIdAsync(roleId);
        if (user is null || role is null || string.IsNullOrEmpty(role.Name)) return ServiceResult<RoleAssignmentDto>.NotFound();

        var existing = await _dbContext.UserRoles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
        if (existing is null) return ServiceResult<RoleAssignmentDto>.NotFound();

        if (!existing.IsActive)
        {
            // Reactivating must respect the same rules as assigning, e.g. an Editor needs a department.
            var ruleError = RoleAssignmentRules.Validate(user.UserName, user.SectionId, new[] { role.Name });
            if (ruleError is not null) return ServiceResult<RoleAssignmentDto>.Invalid(ruleError);

            existing.IsActive = true;
            existing.DateUpdated = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }

        return ServiceResult<RoleAssignmentDto>.Success(ToDto(user, role, existing));
    }

    public async Task<ServiceResult<RoleAssignmentDto>> DeactivateAsync(string id, string? actingUserId)
    {
        if (!RoleAssignmentId.TryParse(id, out var userId, out var roleId)) return ServiceResult<RoleAssignmentDto>.NotFound();

        var user = await _userManager.FindByIdAsync(userId);
        var role = await _roleManager.FindByIdAsync(roleId);
        if (user is null || role is null || string.IsNullOrEmpty(role.Name)) return ServiceResult<RoleAssignmentDto>.NotFound();

        if (role.Name == AppRoles.Admin && userId == actingUserId)
            return ServiceResult<RoleAssignmentDto>.Forbidden("You can't deactivate your own Admin assignment.");

        if (!await DeactivateRowAsync(userId, roleId)) return ServiceResult<RoleAssignmentDto>.NotFound();
        await _dbContext.SaveChangesAsync();

        return ServiceResult<RoleAssignmentDto>.Success((await BuildAsync(user, role))!);
    }

    /// <summary>The reason this user cannot take this role, or null when they can.</summary>
    private async Task<string?> CheckCanHoldAsync(ApplicationUser user, ApplicationRole role)
    {
        if (await _userManager.IsInRoleAsync(user, role.Name!))
            return $"'{user.UserName}' is already assigned to role '{role.Name}'.";

        return RoleAssignmentRules.Validate(user.UserName, user.SectionId, new[] { role.Name });
    }

    /// <summary>Adds the user-role row, or reactivates a deactivated one, preserving its CreatedAt. Does not save.</summary>
    private async Task AddOrReactivateAsync(string userId, string roleId)
    {
        var existing = await _dbContext.UserRoles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (existing is null)
        {
            _dbContext.UserRoles.Add(new ApplicationUserRole { UserId = userId, RoleId = roleId });
        }
        else
        {
            existing.IsActive = true;
            existing.DateUpdated = DateTime.UtcNow;
        }
    }

    /// <summary>Marks an active user-role row inactive. Returns false when there is no active row. Does not save.</summary>
    private async Task<bool> DeactivateRowAsync(string userId, string roleId)
    {
        var userRole = await _dbContext.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
        if (userRole is null) return false;

        userRole.IsActive = false;
        userRole.DateUpdated = DateTime.UtcNow;
        return true;
    }

    private async Task<RoleAssignmentDto?> BuildAsync(ApplicationUser user, ApplicationRole role)
    {
        var userRole = await _dbContext.UserRoles.IgnoreQueryFilters()
            .FirstOrDefaultAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id);

        return userRole is null ? null : ToDto(user, role, userRole);
    }

    private static RoleAssignmentDto ToDto(ApplicationUser user, ApplicationRole role, ApplicationUserRole userRole) => new()
    {
        Id = RoleAssignmentId.Combine(user.Id, role.Id),
        UserId = user.Id,
        UserName = DisplayName(user.FirstName, user.LastName, user.UserName),
        RoleId = role.Id,
        RoleName = role.Name ?? string.Empty,
        IsActive = userRole.IsActive,
        CreatedAt = userRole.CreatedAt,
        DateUpdated = userRole.DateUpdated
    };

    // Falls back to the login name so a user without a first or last name never shows as blank.
    private static string DisplayName(string? firstName, string? lastName, string? loginName)
    {
        var fullName = $"{firstName} {lastName}".Trim();
        return fullName.Length > 0 ? fullName : loginName ?? string.Empty;
    }
}
