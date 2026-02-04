// ViewModel for clinician landing: search query + list of patient rows.


using System;

namespace Software_Engineering_2328668.Models.ViewModels
{
    // ViewModel for the clinician landing page (search + patient rows).
    public class ClinicianLandingViewModel
    {
        public string? Query { get; set; }  // search text from the form
        public List<PatientRow> Rows { get; set; } = new();

        public class PatientRow
        {
            public int Pid { get; set; }
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public DateTime? LastVisitedAt { get; set; }

            public string FullName => $"{FirstName} {LastName}".Trim();
            public string LastVisitedLabel =>
                LastVisitedAt.HasValue ? LastVisitedAt.Value.ToString("yyyy-MM-dd HH:mm") : "—";
        }
    }
}
