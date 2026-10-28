using AAExamManagementSystem.Models.Dtos;
using AAExamManagementSystem.Models.Entities;
using AAExamManagementSystem.Repository;
using AAExamManagementSystem.Services;
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
    private readonly IRegistrationNotifier _registrationNotifier;

    public SignupModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IGenericRepository<Applicant> applicantRepository,
        IMapper mapper,
        ApplicationDbContext context,
        IRegistrationNotifier registrationNotifier)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _applicantRepository = applicantRepository;
        _mapper = mapper;
        _context = context;
        _registrationNotifier = registrationNotifier;
    }

    [BindProperty]
    public ApplicantRegisterDto Input { get; set; } = new();

    /// <summary>Departments an applicant can pick: active ones that have exam questions, except the shared General Knowledge.</summary>
    public IList<string> ChoiceOptions { get; private set; } = new List<string>();

    /// <summary>The start times an applicant can book an exam at.</summary>
    public IReadOnlyList<TimeOnly> ExamSlots => ExamSchedule.Slots;

    public async Task OnGetAsync()
    {
        await LoadChoiceOptionsAsync();
    }

    // Lets the wizard flag a student number that is already registered before the applicant reaches the last step.
    public async Task<IActionResult> OnPostCheckStudentNoAsync(string? studentNo)
    {
        return new JsonResult(new { available = !await StudentNoTakenAsync(studentNo) });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadChoiceOptionsAsync();
        ExamChoicePlanner.Validate(ModelState, Input, ChoiceOptions.ToList());
        ValidateSchool();
        await ValidateStudentNoAsync();

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
            foreach (var attempt in await ExamChoicePlanner.BuildAttemptsAsync(_context, Input, applicant.AcademicYear))
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

        var exams = applicant.Attempts
            .Select(a => a.Section)
            .OfType<string>()
            .ToList();
        _registrationNotifier.NotifyRegistration(
            applicant.FirstName,
            applicant.LastName,
            applicant.Email,
            applicant.StudentNo,
            applicant.AcademicYear,
            exams);

        await _signInManager.SignInAsync(user, isPersistent: false);

        TempData[SignupSuccessModel.SubmittedNameKey] = $"{applicant.FirstName} {applicant.LastName}".Trim();
        TempData[SignupSuccessModel.SubmittedEmailKey] = applicant.Email;

        return RedirectToPage("/SignupSuccess");
    }

    // The school comes from a dropdown, but a posted form can say anything, so check it against the same list.
    private void ValidateSchool()
    {
        var trimmed = Input.School?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            Input.School = null;
            return;
        }

        var match = BenildeSchools.All.FirstOrDefault(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.School)}", "Please pick a school from the list.");
        }

        Input.School = match;
    }

    private async Task ValidateStudentNoAsync()
    {
        Input.StudentNo = Input.StudentNo?.Trim() ?? string.Empty;

        if (Input.StudentNo.Length > 0 && await StudentNoTakenAsync(Input.StudentNo))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.StudentNo)}", "That student number is already registered.");
        }
    }

    // Student numbers are compared ignoring case and surrounding spaces, and inactive applicants still count.
    private async Task<bool> StudentNoTakenAsync(string? studentNo)
    {
        var normalized = studentNo?.Trim().ToLower();
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        return await _context.Applicants.AnyAsync(a => a.StudentNo.Trim().ToLower() == normalized);
    }

    private async Task LoadChoiceOptionsAsync()
    {
        ChoiceOptions = await _context.Sections
            .AsNoTracking()
            .Where(s => s.Name != AAExamManagementSystem.Models.Entities.Departments.GeneralKnowledge
                && s.Questions.Any(q => q.IsActive))
            .OrderBy(s => s.Name)
            .Select(s => s.Name)
            .ToListAsync();
    }
}
