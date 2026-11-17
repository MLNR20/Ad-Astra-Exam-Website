using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Services;

/// <summary>
/// Keeps every question to a single correct answer: a question can have only one choice that is both active and
/// marked correct. A second correct choice is allowed only while the first is inactive (or marked incorrect), so
/// swapping the answer means deactivating the old one first.
/// </summary>
public class ActiveCorrectChoiceValidator
{
    public const string Message =
        "This question already has an active correct choice. Deactivate it or mark it incorrect first.";

    private readonly ApplicationDbContext _context;

    public ActiveCorrectChoiceValidator(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// True when saving the choice as <paramref name="isCorrect"/> and <paramref name="isActive"/> would not give any
    /// question it belongs to a second active correct choice. A choice that ends up inactive or incorrect never
    /// conflicts, and neither does one that isn't linked to a question yet.
    /// </summary>
    public async Task<bool> IsAllowedAsync(Guid choiceId, bool isCorrect, bool isActive)
    {
        if (!isCorrect || !isActive)
        {
            return true;
        }

        var questionIds = _context.QuestionAndChoices
            .Where(qc => qc.ChoiceId == choiceId)
            .Select(qc => qc.QuestionId);

        return !await _context.QuestionAndChoices
            .AnyAsync(qc => questionIds.Contains(qc.QuestionId)
                            && qc.ChoiceId != choiceId
                            && qc.Choice.IsActive
                            && qc.Choice.IsCorrect);
    }

    /// <summary>Adds an error under <paramref name="key"/> when the save would leave a question with two active correct choices.</summary>
    public async Task<bool> ValidateAsync(ModelStateDictionary modelState, string key, Guid choiceId, bool isCorrect, bool isActive)
    {
        if (await IsAllowedAsync(choiceId, isCorrect, isActive))
        {
            return true;
        }

        modelState.AddModelError(key, Message);
        return false;
    }
}
