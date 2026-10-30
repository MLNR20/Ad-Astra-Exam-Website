namespace AAExamManagementSystem.Models.Entities;

public class Section
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public string? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    public ICollection<Question> Questions { get; set; } = new List<Question>();
}
