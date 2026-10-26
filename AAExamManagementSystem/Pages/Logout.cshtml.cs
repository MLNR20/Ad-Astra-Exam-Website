using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AAExamManagementSystem.Pages
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly IRefreshTokenService _refreshTokens;

        public LogoutModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ApplicationDbContext db, IRefreshTokenService refreshTokens)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _db = db;
            _refreshTokens = refreshTokens;
        }

        public IActionResult OnGet()
        {
            return RedirectToPage("/Login");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (Request.Cookies.TryGetValue(RefreshTokenSettings.CookieName, out var refreshToken))
            {
                await _refreshTokens.RevokeAsync(refreshToken);
            }

            RefreshTokenCookies.Delete(HttpContext);
            await _signInManager.SignOutAsync();

            if (user is not null)
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    Type = "Logout",
                    ResponseBody = $"Logout succeeded for '{user.UserName}'.",
                    IsActive = true,
                    CreatedById = user.Id,
                });
                await _db.SaveChangesAsync();
            }

            return RedirectToPage("/Login");
        }
    }
}
