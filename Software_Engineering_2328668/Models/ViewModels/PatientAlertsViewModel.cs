using System;
using System.Collections.Generic;

namespace Software_Engineering_2328668.Models.ViewModels
{
    // ViewModel for patient alerts list (patient-scoped)
    public class PatientAlertsViewModel
    {
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string SensoreId { get; set; } = string.Empty;

        public string Sort { get; set; } = "newest"; // or "oldest"

        public List<AlertRow> Rows { get; set; } = new();

        public class AlertRow
        {
            public int AlertId { get; set; }
            public DateTime TriggeredUtc { get; set; }
            public int AboveForSeconds { get; set; }
            public int ThresholdAu { get; set; }
            public string Severity { get; set; } = "high";
            public DateTime? DatasetDate { get; set; }
            public int StartFrameIndex { get; set; }

            public string TriggeredLocal => TriggeredUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            public string DatasetDateStr => DatasetDate?.ToString("yyyy-MM-dd") ?? "";
        }
    }
}
