// 1:1 profile row for each patient user; holds Sensore ID and clinical thresholds (AU).

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Software_Engineering_2328668.Models
{
    [Table("patient_profile")]
    public class PatientProfile
    {
        [Key]
        [Column("patient_id")]                 // PK and FK to users.user_id (1:1)
        public int PatientId { get; set; }

        [Required, Column("sensore_id")]
        public string SensoreId { get; set; } = string.Empty;   // UNIQUE

        [Column("first_name")]
        public string? FirstName { get; set; }

        [Column("last_name")]
        public string? LastName { get; set; }

        [Column("dob")]
        public DateTime? Dob { get; set; }

        [Column("weight_kg")]
        public decimal? WeightKg { get; set; }

        [Column("base_seating_threshold_au")]
        public int? BaseSeatingThresholdAu { get; set; }

        [Column("alert_threshold_au")]
        public int? AlertThresholdAu { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }
    }
}
