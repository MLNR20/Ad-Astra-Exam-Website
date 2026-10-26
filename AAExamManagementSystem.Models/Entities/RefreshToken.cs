using System.ComponentModel.DataAnnotations;

namespace AAExamManagementSystem.Models.Entities;

public class RefreshToken
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    // SHA-256 of the opaque token handed to the browser; the raw value is never stored.
    public string TokenHash { get; set; } = string.Empty;

    public bool IsPersistent { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
}
