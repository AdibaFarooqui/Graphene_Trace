// 1:1 with Frame; calculated stats for quick UI.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Software_Engineering_2328668.Models
{
    [Table("frame_metrics")]
    public class FrameMetrics
    {
        [Key]
        [Column("frame_id")]
        public long FrameId { get; set; }  // PK and FK to frames.frame_id

        [Column("peak_pressure_au")]
        public decimal? PeakPressureAu { get; set; }

        [Column("avg_pressure_au")]
        public decimal? AvgPressureAu { get; set; }

        [Column("contact_area_px")]
        public int? ContactAreaPx { get; set; }

        [Column("contact_area_pct")]
        public decimal? ContactAreaPct { get; set; }

        [Column("cov_percent")]
        public decimal? CovPercent { get; set; }

        [Column("ppi_au_10s")]
        public decimal? PpiAu10s { get; set; }
    }
}
