using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Pages;
using AAExamManagementSystem.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace AAExamManagementSystem.Tests;

public class RefreshTokenTests
{
    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Fixture
    {
        public ManualTime Time { get; } = new();
        public AAExamManagementSystem.Repository.ApplicationDbContext Db { get; } = TestSupport.CreateContext();
        public RefreshTokenService Service { get; }
        public ApplicationUser User { get; } = new() { Id = "u1", UserName = "jdoe" };

        public Fixture()
        {
            Service = new RefreshTokenService(Db, Options.Create(new RefreshTokenSettings()), Time);
            Db.Users.Add(User);
            Db.SaveChanges();
        }
    }

    [Fact]
    public async Task Issue_StoresOnlyAHashAndExpiresInSevenDays()
    {
        var f = new Fixture();

        var (token, expiresAt) = await f.Service.IssueAsync(f.User, isPersistent: true);

        var stored = Assert.Single(await f.Db.RefreshTokens.ToListAsync());
        Assert.NotEqual(token, stored.TokenHash);
        Assert.Equal(f.Time.Now.UtcDateTime.AddDays(7), expiresAt);
        Assert.Equal(expiresAt, stored.ExpiresAt);
    }

    [Fact]
    public async Task Rotate_WithAValidToken_RevokesItAndReturnsAReplacementWithTheSameExpiry()
    {
        var f = new Fixture();
        var (token, expiresAt) = await f.Service.IssueAsync(f.User, isPersistent: true);
        f.Time.Now = f.Time.Now.AddDays(3);

        var session = await f.Service.RotateAsync(token);

        Assert.NotNull(session);
        Assert.Equal(f.User.Id, session.User.Id);
        Assert.NotEqual(token, session.NewToken);
        Assert.Equal(expiresAt, session.ExpiresAt);
        Assert.True(session.IsPersistent);
        Assert.Equal(1, await f.Db.RefreshTokens.CountAsync(t => t.RevokedAt == null));
    }

    [Fact]
    public async Task Rotate_WithAnExpiredToken_ReturnsNull()
    {
        var f = new Fixture();
        var (token, _) = await f.Service.IssueAsync(f.User, isPersistent: false);
        f.Time.Now = f.Time.Now.AddDays(7).AddSeconds(1);

        Assert.Null(await f.Service.RotateAsync(token));
    }

    [Fact]
    public async Task Rotate_WithAnUnknownToken_ReturnsNull()
    {
        var f = new Fixture();

        Assert.Null(await f.Service.RotateAsync("not-a-real-token"));
    }

    [Fact]
    public async Task Rotate_ReusingAnAlreadyRotatedToken_RevokesEveryTokenForTheUser()
    {
        var f = new Fixture();
        var (token, _) = await f.Service.IssueAsync(f.User, isPersistent: true);
        var session = await f.Service.RotateAsync(token);
        Assert.NotNull(session);

        Assert.Null(await f.Service.RotateAsync(token));

        Assert.All(await f.Db.RefreshTokens.ToListAsync(), t => Assert.NotNull(t.RevokedAt));
        Assert.Null(await f.Service.RotateAsync(session.NewToken));
    }

    [Fact]
    public async Task Revoke_MakesTheTokenUnusable()
    {
        var f = new Fixture();
        var (token, _) = await f.Service.IssueAsync(f.User, isPersistent: true);

        await f.Service.RevokeAsync(token);

        Assert.Null(await f.Service.RotateAsync(token));
    }

    [Fact]
    public async Task RevokeAll_RevokesOnlyThatUsersTokens()
    {
        var f = new Fixture();
        var other = new ApplicationUser { Id = "u2", UserName = "other" };
        f.Db.Users.Add(other);
        await f.Db.SaveChangesAsync();
        await f.Service.IssueAsync(f.User, true);
        await f.Service.IssueAsync(f.User, false);
        var (otherToken, _) = await f.Service.IssueAsync(other, true);

        await f.Service.RevokeAllAsync(f.User.Id);

        Assert.NotNull(await f.Service.RotateAsync(otherToken));
        Assert.Equal(0, await f.Db.RefreshTokens.CountAsync(t => t.UserId == f.User.Id && t.RevokedAt == null));
    }

    [Fact]
    public async Task Issue_ClearsThatUsersExpiredAndRevokedTokens()
    {
        var f = new Fixture();
        var (old, _) = await f.Service.IssueAsync(f.User, true);
        await f.Service.RevokeAsync(old);
        f.Time.Now = f.Time.Now.AddDays(1);

        await f.Service.IssueAsync(f.User, true);

        Assert.Single(await f.Db.RefreshTokens.ToListAsync());
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshTokenAndClearsTheCookie()
    {
        var f = new Fixture();
        var (token, _) = await f.Service.IssueAsync(f.User, true);
        var users = TestSupport.UserManagerReturning(f.User);
        var signIn = TestSupport.CreateSignInManager(users.Object);
        var model = new LogoutModel(signIn.Object, users.Object, f.Db, f.Service);
        TestSupport.Attach(model, TestSupport.PrincipalFor());
        model.Request.Headers.Cookie = $"{RefreshTokenSettings.CookieName}={Uri.EscapeDataString(token)}";

        var result = await model.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.NotNull((await f.Db.RefreshTokens.SingleAsync()).RevokedAt);
        var setCookie = model.Response.Headers.SetCookie.ToString();
        Assert.Contains($"{RefreshTokenSettings.CookieName}=;", setCookie);
    }

    [Fact]
    public async Task Middleware_WithAnAlreadyAuthenticatedUser_LeavesTheRefreshTokenAlone()
    {
        var f = new Fixture();
        var (token, _) = await f.Service.IssueAsync(f.User, true);
        var context = new DefaultHttpContext { User = TestSupport.PrincipalFor() };
        context.Request.Headers.Cookie = $"{RefreshTokenSettings.CookieName}={Uri.EscapeDataString(token)}";
        var nextCalled = false;
        var middleware = new RefreshTokenMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var users = TestSupport.CreateUserManager();
        var signIn = TestSupport.CreateSignInManager(users.Object);

        await middleware.InvokeAsync(context, f.Service, signIn.Object);

        Assert.True(nextCalled);
        Assert.Null((await f.Db.RefreshTokens.SingleAsync()).RevokedAt);
    }

    [Fact]
    public async Task Middleware_WithAnInvalidRefreshCookie_ClearsItAndContinuesAnonymously()
    {
        var f = new Fixture();
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{RefreshTokenSettings.CookieName}=bogus";
        var nextCalled = false;
        var middleware = new RefreshTokenMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var users = TestSupport.CreateUserManager();
        var signIn = TestSupport.CreateSignInManager(users.Object);

        await middleware.InvokeAsync(context, f.Service, signIn.Object);

        Assert.True(nextCalled);
        Assert.False(context.User.Identity?.IsAuthenticated);
        Assert.Contains($"{RefreshTokenSettings.CookieName}=;", context.Response.Headers.SetCookie.ToString());
    }
}
