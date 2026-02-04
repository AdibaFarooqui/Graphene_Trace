// System-generated intervals where pressure exceeded threshold.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Software_Engineering_2328668.Models
{
    [Table("alerts")]
    public class Alert
    {
        [Key]
        [Column("alert_id")]
        public int AlertId { get; set; }

        [Column("patient_id")]
        public int PatientId { get; set; }

        [Column("dataset_id")]
        public int DatasetId { get; set; }

        [Column("triggered_ts_utc")]
        public DateTime TriggeredTsUtc { get; set; }

        [Column("start_frame_index")]
        public int StartFrameIndex { get; set; }

        [Column("end_frame_index")]
        public int EndFrameIndex { get; set; }

        [Column("threshold_au")]
        public int ThresholdAu { get; set; }

        [Column("above_for_seconds")]
        public int AboveForSeconds { get; set; }

        [Column("severity")]
        public string Severity { get; set; } = "high";

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }
    }
}
