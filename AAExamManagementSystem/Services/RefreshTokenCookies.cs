namespace AAExamManagementSystem.Services;

public static class RefreshTokenCookies
{
    public static void Append(HttpContext context, string token, DateTime expiresAt, bool isPersistent)
    {
        context.Response.Cookies.Append(RefreshTokenSettings.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = isPersistent ? new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)) : null,
        });
    }

    public static void Delete(HttpContext context) =>
        context.Response.Cookies.Delete(
            RefreshTokenSettings.CookieName,
            new CookieOptions { Secure = true, SameSite = SameSiteMode.Lax });
}
