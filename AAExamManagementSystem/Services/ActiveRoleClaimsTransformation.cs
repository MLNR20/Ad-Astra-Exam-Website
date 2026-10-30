using System.Security.Claims;
using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Services;

/// <summary>
/// Drops the role claims a signed-in user no longer holds in effect: a role that was deactivated, or an assignment
/// that was deactivated. Runs on every request, so revoking takes effect at once instead of at the next sign-in.
/// </summary>
public class ActiveRoleClaimsTransformation : IClaimsTransformation
{
    private readonly ApplicationDbContext _context;

    public ActiveRoleClaimsTransformation(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return principal;
        }

        var activeRoles = await _context.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.IsActive)
            .Join(_context.Roles.Where(r => r.IsActive), ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .ToListAsync();
        var active = activeRoles.OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);

        var clone = principal.Clone();
        foreach (var identity in clone.Identities)
        {
            foreach (var claim in identity.FindAll(identity.RoleClaimType).ToList())
            {
                if (!active.Contains(claim.Value))
                {
                    identity.RemoveClaim(claim);
                }
            }
        }

        return clone;
    }
}
