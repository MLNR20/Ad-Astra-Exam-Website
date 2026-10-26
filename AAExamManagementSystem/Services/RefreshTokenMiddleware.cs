using AAExamManagementSystem.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace AAExamManagementSystem.Services;

// Runs after UseAuthentication. When the short-lived auth cookie has lapsed but the browser still holds a
// valid refresh token, signs the user back in and rotates the token so the request carries on unnoticed.
public class RefreshTokenMiddleware
{
    private readonly RequestDelegate _next;

    public RefreshTokenMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        IRefreshTokenService tokens,
        SignInManager<ApplicationUser> signInManager)
    {
        if (context.User.Identity?.IsAuthenticated != true
            && context.Request.Cookies.TryGetValue(RefreshTokenSettings.CookieName, out var token)
            && !string.IsNullOrEmpty(token))
        {
            var session = await tokens.RotateAsync(token);

            if (session is not null
                && await signInManager.CanSignInAsync(session.User)
                && !await signInManager.UserManager.IsLockedOutAsync(session.User))
            {
                var principal = await signInManager.CreateUserPrincipalAsync(session.User);
                await context.SignInAsync(
                    IdentityConstants.ApplicationScheme,
                    principal,
                    new AuthenticationProperties { IsPersistent = session.IsPersistent });

                RefreshTokenCookies.Append(context, session.NewToken, session.ExpiresAt, session.IsPersistent);
                context.User = principal;
            }
            else
            {
                if (session is not null)
                {
                    await tokens.RevokeAsync(session.NewToken);
                }

                RefreshTokenCookies.Delete(context);
            }
        }

        await _next(context);
    }
}
