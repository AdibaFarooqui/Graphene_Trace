using System;

namespace Software_Engineering_2328668.Models.ViewModels
{
    // ViewModel for patient profile (details page). Only Notes is editable in MVP.
    public class PatientDetailsViewModel
    {
        public int PatientId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public DateTime? Dob { get; set; }
        public decimal? WeightKg { get; set; }
        public string SensoreId { get; set; } = "";
        public int? BaseSeatingThresholdAu { get; set; }
        public int? AlertThresholdAu { get; set; }

        // Editable
        public string Notes { get; set; } = "";
    }
}
