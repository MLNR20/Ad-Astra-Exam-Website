using AppRoles = AAExamManagementSystem.Models.Entities.Roles;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Pages;

/// <summary>The applicant's own record and every exam they have booked, newest application first.</summary>
[Authorize(Roles = AppRoles.Applicant)]
public class ApplicantDetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ApplicantDetailsModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public Applicant? Applicant { get; private set; }

    public IList<Attempt> Bookings { get; private set; } = new List<Attempt>();

    public async Task OnGetAsync()
    {
        var userId = _userManager.GetUserId(User);
        Applicant = await _context.Applicants
            .AsNoTracking()
            .Include(a => a.Attempts)
            .FirstOrDefaultAsync(a => a.UserId == userId);

        Bookings = Applicant?.Attempts
            .OrderByDescending(a => a.Id)
            .ToList() ?? new List<Attempt>();
    }
}
