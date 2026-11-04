using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GLORIOUSSYSTEM.Data.Models;

[Table("Users")]
public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public int IsActive { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public string? Role { get; set; } = "User";

    // Incrementing this value invalidates every previously issued API session.
    public int SessionVersion { get; set; } = 1;

    [MaxLength(128)]
    public string? RefreshTokenHash { get; set; }

    public DateTime? RefreshTokenExpiresAt { get; set; }

    [MaxLength(128)]
    public string? PasswordResetTokenHash { get; set; }

    public DateTime? PasswordResetExpiresAt { get; set; }
}
