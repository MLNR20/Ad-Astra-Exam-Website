using System.Security.Cryptography;
using System.Text;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAExamManagementSystem.Services;

public class RefreshTokenSettings
{
    public const string CookieName = "ems.refresh";

    public int LifetimeDays { get; set; } = 7;
}

public record RefreshedSession(ApplicationUser User, string NewToken, DateTime ExpiresAt, bool IsPersistent);

public interface IRefreshTokenService
{
    Task<(string Token, DateTime ExpiresAt)> IssueAsync(ApplicationUser user, bool isPersistent);

    // Single-use: a valid token is revoked and replaced. Presenting an already-revoked token
    // is treated as theft and revokes every token the user holds.
    Task<RefreshedSession?> RotateAsync(string token);

    Task RevokeAsync(string token);

    Task RevokeAllAsync(string userId);
}

public class RefreshTokenService : IRefreshTokenService
{
    private readonly ApplicationDbContext _db;
    private readonly RefreshTokenSettings _settings;
    private readonly TimeProvider _time;

    public RefreshTokenService(ApplicationDbContext db, IOptions<RefreshTokenSettings> settings, TimeProvider time)
    {
        _db = db;
        _settings = settings.Value;
        _time = time;
    }

    public async Task<(string Token, DateTime ExpiresAt)> IssueAsync(ApplicationUser user, bool isPersistent)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var now = _time.GetUtcNow().UtcDateTime;
        var entity = new RefreshToken
        {
            TokenHash = Hash(token),
            UserId = user.Id,
            IsPersistent = isPersistent,
            DateCreated = now,
            ExpiresAt = now.AddDays(_settings.LifetimeDays),
        };

        // Expired and revoked rows are dead weight; clear this user's on each new login.
        var stale = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && (t.ExpiresAt <= now || t.RevokedAt != null))
            .ToListAsync();
        _db.RefreshTokens.RemoveRange(stale);

        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync();

        return (token, entity.ExpiresAt);
    }

    public async Task<RefreshedSession?> RotateAsync(string token)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var hash = Hash(token);
        var existing = await _db.RefreshTokens.Include(t => t.User).SingleOrDefaultAsync(t => t.TokenHash == hash);

        if (existing is null || existing.User is null)
        {
            return null;
        }

        if (existing.RevokedAt is not null)
        {
            await RevokeAllAsync(existing.UserId);
            return null;
        }

        if (existing.ExpiresAt <= now)
        {
            return null;
        }

        existing.RevokedAt = now;

        // The replacement keeps the original expiry so a session cannot be extended past 7 days
        // from the login that created it.
        var replacement = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        _db.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = Hash(replacement),
            UserId = existing.UserId,
            IsPersistent = existing.IsPersistent,
            DateCreated = now,
            ExpiresAt = existing.ExpiresAt,
        });
        await _db.SaveChangesAsync();

        return new RefreshedSession(existing.User, replacement, existing.ExpiresAt, existing.IsPersistent);
    }

    public async Task RevokeAsync(string token)
    {
        var hash = Hash(token);
        var existing = await _db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash);
        if (existing is not null && existing.RevokedAt is null)
        {
            existing.RevokedAt = _time.GetUtcNow().UtcDateTime;
            await _db.SaveChangesAsync();
        }
    }

    public async Task RevokeAllAsync(string userId)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var active = await _db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync();
        foreach (var token in active)
        {
            token.RevokedAt = now;
        }

        await _db.SaveChangesAsync();
    }

    private static string Hash(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
