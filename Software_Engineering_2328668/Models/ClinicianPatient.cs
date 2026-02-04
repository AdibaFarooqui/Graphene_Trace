// M:N assignment join between clinician accounts and patient accounts. Unique (clinician_id, patient_id).

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Software_Engineering_2328668.Models
{
    [Table("clinician_patient")]
    public class ClinicianPatient
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }                       // surrogate PK

        [Column("clinician_id")]
        public int ClinicianId { get; set; }              // FK -> users.user_id (clinician)

        [Column("patient_id")]
        public int PatientId { get; set; }                // FK -> users.user_id (patient)

        [Column("assigned_at")]
        public DateTime? AssignedAt { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("last_visited_at")]
        public DateTime? LastVisitedAt { get; set; }
    }
}
