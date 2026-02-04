using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Software_Engineering_2328668.Models
{
    [Table("users")]  // maps to MySQL 'users' table
    public class UserAccount
    {
        [Key]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required, Column("username")]
        public string Username { get; set; } = string.Empty;

        [Required, Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("email")]
        public string? Email { get; set; }

        // 'clinician' | 'patient' | 'admin'
        [Required, Column("role")]
        public string Role { get; set; } = "patient";

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}

