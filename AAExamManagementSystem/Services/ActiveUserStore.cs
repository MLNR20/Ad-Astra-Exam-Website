using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Services;

/// <summary>
/// The Identity user store, with username and email lookups limited to users that are not archived. An archived
/// user keeps its username and email for history but no longer holds them, so a new user can reuse them; without
/// this, <c>FindByNameAsync</c> could return the archived account and <c>FindByEmailAsync</c> would throw on the
/// two matching rows. Lookups by id are unchanged so history still resolves archived users.
/// </summary>
public class ActiveUserStore : UserStore<ApplicationUser, ApplicationRole, ApplicationDbContext, string,
    IdentityUserClaim<string>, ApplicationUserRole, IdentityUserLogin<string>, IdentityUserToken<string>,
    IdentityRoleClaim<string>>
{
    public ActiveUserStore(ApplicationDbContext context, IdentityErrorDescriber? describer = null)
        : base(context, describer)
    {
    }

    public override Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return Users.FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName && !u.IsDeleted, cancellationToken);
    }

    public override Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail && !u.IsDeleted, cancellationToken);
    }
}
