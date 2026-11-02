using System.ComponentModel.DataAnnotations;

namespace AAExamManagementSystem.Models.Validation;

public enum NamedEntityKind
{
    Department,
    Section,
    Course
}

/// <summary>
/// Marks a name that may be used by only one active record of its kind. The check needs the database and the
/// record being edited, so the server enforces it through ActiveNameValidator; this attribute is what makes the
/// browser check the name as the user types.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UniqueActiveNameAttribute : ValidationAttribute
{
    public UniqueActiveNameAttribute(NamedEntityKind kind)
        : base($"An active {kind.ToString().ToLowerInvariant()} with this name already exists.")
    {
        Kind = kind;
    }

    public NamedEntityKind Kind { get; }

    public override bool IsValid(object? value) => true;
}
