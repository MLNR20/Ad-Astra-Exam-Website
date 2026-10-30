using Microsoft.AspNetCore.Identity;

namespace AAExamManagementSystem.Models.Entities;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Title { get; set; }

    public string? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public int? SectionId { get; set; }
    public Section? Section { get; set; }

    public int? CourseId { get; set; }
    public Course? Course { get; set; }

    // Deleting a user locks the account and hides it; the row stays so history keeps resolving it.
    public bool IsDeleted { get; set; }

    public Applicant? Applicant { get; set; }
}
