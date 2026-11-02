using System.ComponentModel.DataAnnotations;
using AAExamManagementSystem.Models.Validation;

namespace AAExamManagementSystem.Models.Dtos;

public class SectionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime DateCreated { get; set; }
}

public class SectionCreateUpdateDto
{
    [Required, StringLength(100)]
    [RegularExpression(NameRules.LettersOnlyPattern, ErrorMessage = NameRules.LettersOnlyMessage)]
    [UniqueActiveName(NamedEntityKind.Section)]
    public string Name { get; set; } = string.Empty;
}
