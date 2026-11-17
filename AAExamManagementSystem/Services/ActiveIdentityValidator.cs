using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Services;

/// <summary>
/// Keeps usernames, emails and student numbers unique among active records only. A user is archived by deleting
/// it (<c>IsDeleted</c>) and an applicant by deactivating it (<c>IsActive = false</c>); archived records keep their
/// username, email and student number for history but no longer hold them, so a new record can reuse any of them.
/// Values compare ignoring case and surrounding spaces. A record being edited never blocks itself.
/// </summary>
public class ActiveIdentityValidator
{
    private readonly ApplicationDbContext _context;

    public ActiveIdentityValidator(ApplicationDbContext context)
    {
        _context = context;
    }

    public static string UserNameMessage(string userName) => $"The username '{userName}' is already in use.";

    public static string EmailMessage(string email) => $"The email '{email}' is already in use.";

    public static string StudentNoMessage(string studentNo) => $"The student number '{studentNo}' is already registered.";

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    /// <summary>True when no active user holds the username. <paramref name="excludeUserId"/> is the user being edited.</summary>
    public async Task<bool> IsUserNameAvailableAsync(string? userName, string? excludeUserId = null)
    {
        var normalized = Normalize(userName);
        if (normalized.Length == 0)
        {
            return true;
        }

        return !await _context.Users
            .AnyAsync(u => !u.IsDeleted
                           && u.Id != excludeUserId
                           && u.UserName != null
                           && u.UserName.Trim().ToLower() == normalized);
    }

    /// <summary>
    /// True when neither an active user nor an active applicant holds the email. An applicant's email can differ from
    /// their account's, so both are checked; the applicant belonging to <paramref name="excludeUserId"/> is skipped.
    /// </summary>
    public async Task<bool> IsEmailAvailableAsync(string? email, string? excludeUserId = null, int? excludeApplicantId = null)
    {
        var normalized = Normalize(email);
        if (normalized.Length == 0)
        {
            return true;
        }

        var heldByUser = await _context.Users
            .AnyAsync(u => !u.IsDeleted
                           && u.Id != excludeUserId
                           && u.Email != null
                           && u.Email.Trim().ToLower() == normalized);
        if (heldByUser)
        {
            return false;
        }

        return !await _context.Applicants
            .AnyAsync(a => a.IsActive
                           && a.Id != excludeApplicantId
                           && a.UserId != excludeUserId
                           && a.Email.Trim().ToLower() == normalized);
    }

    /// <summary>True when no active applicant holds the student number. <paramref name="excludeApplicantId"/> is the one being edited.</summary>
    public async Task<bool> IsStudentNoAvailableAsync(string? studentNo, int? excludeApplicantId = null)
    {
        var normalized = Normalize(studentNo);
        if (normalized.Length == 0)
        {
            return true;
        }

        return !await _context.Applicants
            .AnyAsync(a => a.IsActive
                           && a.Id != excludeApplicantId
                           && a.StudentNo.Trim().ToLower() == normalized);
    }

    /// <summary>Adds an error under <paramref name="key"/> when an active user already holds the username.</summary>
    public async Task<bool> ValidateUserNameAsync(
        ModelStateDictionary modelState, string key, string? userName, string? excludeUserId = null)
    {
        if (await IsUserNameAvailableAsync(userName, excludeUserId))
        {
            return true;
        }

        modelState.AddModelError(key, UserNameMessage(userName!.Trim()));
        return false;
    }

    /// <summary>Adds an error under <paramref name="key"/> when an active user or applicant already holds the email.</summary>
    public async Task<bool> ValidateEmailAsync(
        ModelStateDictionary modelState, string key, string? email, string? excludeUserId = null, int? excludeApplicantId = null)
    {
        if (await IsEmailAvailableAsync(email, excludeUserId, excludeApplicantId))
        {
            return true;
        }

        modelState.AddModelError(key, EmailMessage(email!.Trim()));
        return false;
    }

    /// <summary>Adds an error under <paramref name="key"/> when an active applicant already holds the student number.</summary>
    public async Task<bool> ValidateStudentNoAsync(
        ModelStateDictionary modelState, string key, string? studentNo, int? excludeApplicantId = null)
    {
        if (await IsStudentNoAvailableAsync(studentNo, excludeApplicantId))
        {
            return true;
        }

        modelState.AddModelError(key, StudentNoMessage(studentNo!.Trim()));
        return false;
    }
}
