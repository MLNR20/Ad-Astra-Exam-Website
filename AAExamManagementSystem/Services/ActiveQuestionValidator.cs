using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Services;

/// <summary>
/// Keeps questions unique within a section: only one active question can hold a given title at a time. A title can
/// be reused once the question holding it has been deactivated or deleted. Titles compare ignoring case and
/// surrounding or repeated spaces, and the same title in a different section is fine.
/// </summary>
public class ActiveQuestionValidator
{
    private readonly ApplicationDbContext _context;

    public ActiveQuestionValidator(ApplicationDbContext context)
    {
        _context = context;
    }

    public static string Message(string title) =>
        $"An active question titled '{title}' already exists in this section.";

    /// <summary>
    /// True when no other active question in <paramref name="sectionId"/> uses the title.
    /// <paramref name="excludeId"/> is the question being edited, so it never blocks itself.
    /// </summary>
    public async Task<bool> IsAvailableAsync(int sectionId, string? title, Guid? excludeId = null)
    {
        var normalized = ActiveNameValidator.Normalize(title).ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return true;
        }

        return !await _context.Questions.IgnoreQueryFilters()
            .AnyAsync(q => q.IsActive
                           && !q.IsDeleted
                           && q.SectionId == sectionId
                           && q.Id != excludeId
                           && q.QuestionTitle.Trim().ToLower() == normalized);
    }

    /// <summary>
    /// Adds an error under <paramref name="key"/> when the question would be active alongside another active
    /// question with the same title. An inactive question is never checked, since it holds no title.
    /// </summary>
    public async Task<bool> ValidateAsync(
        ModelStateDictionary modelState, string key, int sectionId, string? title, bool isActive, Guid? excludeId = null)
    {
        if (!isActive || await IsAvailableAsync(sectionId, title, excludeId))
        {
            return true;
        }

        modelState.AddModelError(key, Message(ActiveNameValidator.Normalize(title)));
        return false;
    }
}
