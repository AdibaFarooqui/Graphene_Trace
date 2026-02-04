// Carries patient identity and the list of dataset dates into the monitor Razor view.

using System;
using System.Collections.Generic;

namespace Software_Engineering_2328668.Models.ViewModels
{
    // ViewModel for the Patient Monitor page: identity + available dataset dates.
    public class MonitorPageViewModel
    {
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string SensoreId { get; set; } = string.Empty;

        // Dates we have data for (yyyy-MM-dd strings)
        public List<string> AvailableDates { get; set; } = new();

        // Preselected/default date (first available)
        public string? SelectedDate { get; set; }

        // Allowed durations in seconds (UI can show "30s, 1m, 2m, 5m")
        public int[] DurationsSeconds { get; } = new[] { 30, 60, 120, 300 };

        // Default duration (5 minutes)
        public int DefaultDurationSeconds { get; } = 300;
    }
}
