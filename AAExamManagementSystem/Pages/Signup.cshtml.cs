using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Pages;

public class SignupModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IGenericRepository<Applicant> _applicantRepository;
    private readonly IMapper _mapper;
    private readonly ApplicationDbContext _context;

    public SignupModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IGenericRepository<Applicant> applicantRepository,
        IMapper mapper,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _applicantRepository = applicantRepository;
        _mapper = mapper;
        _context = context;
    }

    [BindProperty]
    public ApplicantRegisterDto Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var existingUser = await _userManager.FindByNameAsync(Input.UserName);
        if (existingUser is not null)
        {
            ModelState.AddModelError(string.Empty, "An account with that username already exists.");
            return Page();
        }

        var user = new ApplicationUser
        {
            UserName = Input.UserName,
            Email = Input.Email,
            EmailConfirmed = true,
            FirstName = Input.FirstName,
            LastName = Input.LastName
        };

        var createResult = await _userManager.CreateAsync(user, Input.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        var roleResult = await _userManager.AddToRoleAsync(user, AAExamManagementSystem.Models.Entities.Roles.Applicant);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            ModelState.AddModelError(string.Empty, "Failed to complete registration. Please try again.");
            return Page();
        }

        var applicant = _mapper.Map<Applicant>(Input);
        applicant.Email = Input.Email;
        applicant.UserId = user.Id;

        try
        {
            foreach (var attempt in await BuildAttemptsAsync(applicant))
            {
                applicant.Attempts.Add(attempt);
            }

            await _applicantRepository.AddAsync(applicant);
            await _applicantRepository.SaveChangesAsync();
        }
        catch
        {
            await _userManager.DeleteAsync(user);
            ModelState.AddModelError(string.Empty, "Failed to save your application. Please try again.");
            return Page();
        }

        await _signInManager.SignInAsync(user, isPersistent: false);

        return RedirectToPage("/ApplicantPortal");
    }

    // One attempt per distinct choice, so a second choice gives the applicant a second exam to take.
    private async Task<IList<Attempt>> BuildAttemptsAsync(Applicant applicant)
    {
        var choices = new List<string>();
        foreach (var choice in new[] { applicant.FirstChoice, applicant.SecondChoice })
        {
            var trimmed = choice?.Trim();
            if (!string.IsNullOrEmpty(trimmed)
                && !choices.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                choices.Add(trimmed);
            }
        }

        var attempts = new List<Attempt>();
        var usedPasswords = new HashSet<string>();

        foreach (var choice in choices)
        {
            var sectionName = await _context.Sections
                .Where(s => s.IsActive && s.Name.ToLower() == choice.ToLower())
                .Select(s => s.Name)
                .FirstOrDefaultAsync() ?? choice;

            attempts.Add(new Attempt
            {
                AcademicYear = applicant.AcademicYear,
                Section = sectionName,
                Password = await GeneratePasswordAsync(applicant.AcademicYear, sectionName, usedPasswords),
            });
        }

        return attempts;
    }

    private async Task<string> GeneratePasswordAsync(string academicYear, string sectionName, ISet<string> usedPasswords)
    {
        while (true)
        {
            var password = AttemptPassword.Generate(academicYear, sectionName);
            if (!usedPasswords.Contains(password)
                && !await _context.Attempts.AnyAsync(a => a.Password == password))
            {
                usedPasswords.Add(password);
                return password;
            }
        }
    }
}
