// One row per ingested CSV/run; stores timing and legend stats. Metadata about the dataset as a whole.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Software_Engineering_2328668.Models
{
    [Table("datasets")]
    public class Dataset
    {
        [Key]
        [Column("dataset_id")]
        public int DatasetId { get; set; }

        [Column("patient_id")]
        public int PatientId { get; set; }

        [Column("sensore_id")]
        public string SensoreId { get; set; } = string.Empty;

        [Column("dataset_code")]
        public string DatasetCode { get; set; } = string.Empty;

        [Column("original_name")]
        public string OriginalName { get; set; } = string.Empty;

        [Column("file_date")]
        public DateTime? FileDate { get; set; }

        [Column("start_time_utc")]
        public DateTime? StartTimeUtc { get; set; }

        [Column("start_time_source")]
        public string? StartTimeSource { get; set; }

        [Column("duration_s")]
        public int DurationSeconds { get; set; }

        [Column("frames_count")]
        public int FramesCount { get; set; }

        [Column("fps")]
        public decimal Fps { get; set; }

        [Column("width")]
        public short Width { get; set; }

        [Column("height")]
        public short Height { get; set; }

        [Column("min_au_dataset")]
        public decimal? MinAuDataset { get; set; }

        [Column("max_au_dataset")]
        public decimal? MaxAuDataset { get; set; }

        [Column("p01_au_dataset")]
        public decimal? P01AuDataset { get; set; }

        [Column("p99_au_dataset")]
        public decimal? P99AuDataset { get; set; }

        [Column("storage_path")]
        public string? StoragePath { get; set; }

        [Column("checksum")]
        public string? Checksum { get; set; }

        [Column("ingested_at")]
        public DateTime? IngestedAt { get; set; }
    }
}
