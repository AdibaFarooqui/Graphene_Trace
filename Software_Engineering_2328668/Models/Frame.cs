// One row per frame within a dataset (composition). Stores basic stats and reference to matrix data. 

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Software_Engineering_2328668.Models
{
    [Table("frames")]
    public class Frame
    {
        [Key]
        [Column("frame_id")]
        public long FrameId { get; set; }

        [Column("dataset_id")]
        public int DatasetId { get; set; }

        [Column("frame_index")]
        public int FrameIndex { get; set; }

        [Column("ts_utc")]
        public DateTime? TsUtc { get; set; }

        [Column("min_au")]
        public decimal? MinAu { get; set; }

        [Column("max_au")]
        public decimal? MaxAu { get; set; }

        [Column("mean_au")]
        public decimal? MeanAu { get; set; }

        [Column("std_au")]
        public decimal? StdAu { get; set; }

        [Column("matrix_ref")]
        public string? MatrixRef { get; set; }
    }
}
