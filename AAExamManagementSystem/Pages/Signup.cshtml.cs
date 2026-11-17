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
    private readonly ActiveIdentityValidator _identity;

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
        _identity = new ActiveIdentityValidator(context);
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
        return new JsonResult(new { available = await _identity.IsStudentNoAvailableAsync(studentNo) });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadChoiceOptionsAsync();
        ExamChoicePlanner.Validate(ModelState, Input, ChoiceOptions.ToList());
        ValidateSchool();
        ValidateYearLevel();
        await ValidateStudentNoAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Only active accounts hold a username or email; an archived account's can be reused.
        await _identity.ValidateUserNameAsync(ModelState, $"{nameof(Input)}.{nameof(Input.UserName)}", Input.UserName);
        await _identity.ValidateEmailAsync(ModelState, $"{nameof(Input)}.{nameof(Input.Email)}", Input.Email);
        if (!ModelState.IsValid)
        {
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
        TempData[SignupSuccessModel.SubmittedAtKey] = ExamClock.Now.ToString("MMM d, yyyy h:mm tt");

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

    private void ValidateYearLevel()
    {
        if (string.IsNullOrWhiteSpace(Input.YearLevel))
        {
            Input.YearLevel = null;
            return;
        }

        var match = YearLevels.Normalize(Input.YearLevel);
        if (match is null)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.YearLevel)}", "Please pick a year level from the list.");
        }

        Input.YearLevel = match;
    }

    private async Task ValidateStudentNoAsync()
    {
        Input.StudentNo = Input.StudentNo?.Trim() ?? string.Empty;

        // Student numbers compare ignoring case and surrounding spaces; an archived applicant's can be reused.
        await _identity.ValidateStudentNoAsync(ModelState, $"{nameof(Input)}.{nameof(Input.StudentNo)}", Input.StudentNo);
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
