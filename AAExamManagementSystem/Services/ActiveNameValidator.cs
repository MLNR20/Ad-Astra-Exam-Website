using System.Linq;
using AAExamManagementSystem.Models.Validation;
using AAExamManagementSystem.Repository;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace AAExamManagementSystem.Services;

/// <summary>
/// Keeps department, section and course names unique among active records: a name can be reused only once the
/// record holding it has been deactivated. Names compare ignoring case and surrounding or repeated spaces.
/// </summary>
public class ActiveNameValidator
{
    private readonly ApplicationDbContext _context;

    public ActiveNameValidator(ApplicationDbContext context)
    {
        _context = context;
    }

    public static string Normalize(string? name) =>
        string.Join(' ', (name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string Message(NamedEntityKind kind, string name) =>
        $"An active {kind.ToString().ToLowerInvariant()} named '{name}' already exists.";

    /// <summary>True when no other active record of this kind uses the name. <paramref name="excludeId"/> is the record being edited.</summary>
    public async Task<bool> IsAvailableAsync(NamedEntityKind kind, string? name, string? excludeId = null)
    {
        var normalized = Normalize(name).ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return true;
        }

        switch (kind)
        {
            case NamedEntityKind.Department:
                return !await _context.Departments.IgnoreQueryFilters()
                    .AnyAsync(d => d.IsActive && d.Id != excludeId && d.Name.Trim().ToLower() == normalized);
            case NamedEntityKind.Section:
            {
                var excluded = int.TryParse(excludeId, out var sectionId) ? sectionId : -1;
                return !await _context.Sections.IgnoreQueryFilters()
                    .AnyAsync(s => s.IsActive && s.Id != excluded && s.Name.Trim().ToLower() == normalized);
            }
            case NamedEntityKind.Course:
            {
                var excluded = int.TryParse(excludeId, out var courseId) ? courseId : -1;
                return !await _context.Courses.IgnoreQueryFilters()
                    .AnyAsync(c => c.IsActive && c.Id != excluded && c.Name.Trim().ToLower() == normalized);
            }
            default:
                return true;
        }
    }

    /// <summary>Adds an error under <paramref name="key"/> when the name is already taken by an active record.</summary>
    public async Task<bool> ValidateAsync(ModelStateDictionary modelState, string key, NamedEntityKind kind, string? name, string? excludeId = null)
    {
        if (await IsAvailableAsync(kind, name, excludeId))
        {
            return true;
        }

        modelState.AddModelError(key, Message(kind, Normalize(name)));
        return false;
    }
}
