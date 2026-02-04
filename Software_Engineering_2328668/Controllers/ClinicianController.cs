// Loads identity + dates for the monitor UI and updates last-visited audit. 

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Software_Engineering_2328668.Data;
using Software_Engineering_2328668.Models.ViewModels;

namespace Software_Engineering_2328668.Controllers
{
    [Authorize(Roles = "clinician")]
    public class ClinicianController : Controller
    {
        private readonly AppDbContext _db;
        public ClinicianController(AppDbContext db) => _db = db;

        // ---------------------------
        // Helpers (added for S2)
        // ---------------------------

        // Helper: get current clinician id from cookie claims; throws if missing
        private int CurrentClinicianId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idStr, out var clinicianId))
                throw new InvalidOperationException("No clinician id in auth cookie.");
            return clinicianId;
        }

        // Helper: ensure clinician is assigned to the patient (security gate)
        private Task<bool> IsAssignedAsync(int clinicianId, int patientId) =>
            _db.ClinicianPatients.AnyAsync(cp => cp.ClinicianId == clinicianId && cp.PatientId == patientId && cp.IsActive);

        // Helper: get patient display name and Sensore ID for layout header
        private async Task<(string Name, string SensoreId)> GetPatientIdentityAsync(int patientId)
        {
            var p = await _db.PatientProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.PatientId == patientId);
            if (p == null) return ("", "");
            return ($"{p.FirstName} {p.LastName}".Trim(), p.SensoreId);
        }

        /// <summary>
        /// Landing page: searchable list of assigned patients.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Dashboard(string? q)
        {
            ViewData["Title"] = "My Patients";
            var vm = new ClinicianLandingViewModel { Query = q };

            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idStr, out var clinicianId))
                return Unauthorized("No clinician id in auth cookie.");

            var baseQuery =
                from cp in _db.ClinicianPatients.AsNoTracking()
                where cp.ClinicianId == clinicianId && cp.IsActive
                join u in _db.Users.AsNoTracking() on cp.PatientId equals u.UserId
                where u.Role == "patient" && u.IsActive
                join p in _db.PatientProfiles.AsNoTracking() on u.UserId equals p.PatientId
                select new
                {
                    Pid = u.UserId,
                    p.FirstName,
                    p.LastName,
                    cp.LastVisitedAt
                };

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                bool digitsOnly = term.All(char.IsDigit);

                if (digitsOnly && int.TryParse(term, out var pid))
                    baseQuery = baseQuery.Where(x => x.Pid == pid);
                else
                {
                    var like = $"%{term}%";
                    baseQuery = baseQuery.Where(x =>
                        EF.Functions.Like(x.FirstName!, like) ||
                        EF.Functions.Like(x.LastName!, like));
                }
            }

            var rows = await baseQuery
                .OrderByDescending(x => x.LastVisitedAt)
                .ThenBy(x => x.LastName)
                .Select(x => new ClinicianLandingViewModel.PatientRow
                {
                    Pid = x.Pid,
                    FirstName = x.FirstName ?? string.Empty,
                    LastName = x.LastName ?? string.Empty,
                    LastVisitedAt = x.LastVisitedAt
                })
                .ToListAsync();

            vm.Rows = rows;
            return View(vm);
        }

        /// <summary>
        /// Patient Monitor shell: verifies clinician→patient assignment, loads available dates,
        /// updates LastVisitedAt, and renders the monitor view (canvas/controls wired next phase).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PatientMonitor(int id)
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idStr, out var clinicianId))
                return Unauthorized();

            // Ensure the logged-in clinician is assigned to this patient
            var cp = await _db.ClinicianPatients
                .FirstOrDefaultAsync(x => x.ClinicianId == clinicianId && x.PatientId == id && x.IsActive);
            if (cp == null) return Forbid();

            // Load patient profile (name, sensore id)
            var profile = await _db.PatientProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PatientId == id);
            if (profile == null) return NotFound("Patient profile not found.");

            // Get available dataset dates (distinct yyyy-MM-dd strings)
            var dates = await _db.Datasets.AsNoTracking()
                .Where(d => d.PatientId == id && d.FileDate != null)
                .OrderBy(d => d.FileDate)
                .Select(d => d.FileDate!.Value.ToString("yyyy-MM-dd"))
                .Distinct()
                .ToListAsync();

            // Update last visited for this clinician→patient
            cp.LastVisitedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // --- NEW for S2: pass patient context to the patient layout (sticky left sidebar) ---
            ViewData["PatientId"] = id;
            ViewData["PatientName"] = $"{profile.FirstName} {profile.LastName}".Trim();
            ViewData["SensoreId"] = profile.SensoreId;
            ViewData["ActiveTab"] = "monitor";

            var vm = new MonitorPageViewModel
            {
                PatientId = id,
                PatientName = $"{profile.FirstName} {profile.LastName}".Trim(),
                SensoreId = profile.SensoreId,
                AvailableDates = dates,
                SelectedDate = dates.FirstOrDefault()
            };

            ViewData["Title"] = "Patient Monitor";
            return View(vm);
        }

        // --------------------------------------------------
        // NEW: Alerts page (patient-scoped, with sorting)
        // --------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Alerts(int id, string sort = "newest")
        {
            var clinicianId = CurrentClinicianId();
            if (!await IsAssignedAsync(clinicianId, id)) return Forbid();

            var (name, sensoreId) = await GetPatientIdentityAsync(id);

            // ViewData for patient layout header + active tab
            ViewData["PatientId"] = id;
            ViewData["PatientName"] = name;
            ViewData["SensoreId"] = sensoreId;
            ViewData["ActiveTab"] = "alerts";
            ViewData["Title"] = "Alerts";

            // Query alerts for this patient; join dataset to get the file-date
            var q =
                from a in _db.Alerts.AsNoTracking()
                where a.PatientId == id
                join d in _db.Datasets.AsNoTracking() on a.DatasetId equals d.DatasetId
                select new PatientAlertsViewModel.AlertRow
                {
                    AlertId = a.AlertId,
                    TriggeredUtc = a.TriggeredTsUtc,
                    AboveForSeconds = a.AboveForSeconds,
                    ThresholdAu = a.ThresholdAu,
                    Severity = a.Severity,
                    DatasetDate = d.FileDate,
                    StartFrameIndex = a.StartFrameIndex
                };

            q = sort?.ToLowerInvariant() == "oldest"
                ? q.OrderBy(x => x.TriggeredUtc)
                : q.OrderByDescending(x => x.TriggeredUtc);

            var vm = new PatientAlertsViewModel
            {
                PatientId = id,
                PatientName = name,
                SensoreId = sensoreId,
                Sort = sort,
                Rows = await q.ToListAsync()
            };

            return View(vm);
        }

        // --------------------------------------------------
        // NEW: Patient details (GET view + POST notes save)
        // --------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> PatientDetails(int id)
        {
            var clinicianId = CurrentClinicianId();
            if (!await IsAssignedAsync(clinicianId, id)) return Forbid();

            var p = await _db.PatientProfiles.FirstOrDefaultAsync(x => x.PatientId == id);
            if (p == null) return NotFound();

            // ViewData for patient layout header + active tab
            ViewData["PatientId"] = id;
            ViewData["PatientName"] = $"{p.FirstName} {p.LastName}".Trim();
            ViewData["SensoreId"] = p.SensoreId;
            ViewData["ActiveTab"] = "details";
            ViewData["Title"] = "Patient Details";

            var vm = new PatientDetailsViewModel
            {
                PatientId = id,
                FirstName = p.FirstName ?? "",
                LastName = p.LastName ?? "",
                Dob = p.Dob,
                WeightKg = p.WeightKg,
                SensoreId = p.SensoreId,
                BaseSeatingThresholdAu = p.BaseSeatingThresholdAu,
                AlertThresholdAu = p.AlertThresholdAu,
                Notes = p.Notes ?? ""
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PatientDetails(int id, PatientDetailsViewModel vm)
        {
            var clinicianId = CurrentClinicianId();
            if (!await IsAssignedAsync(clinicianId, id)) return Forbid();

            var p = await _db.PatientProfiles.FirstOrDefaultAsync(x => x.PatientId == id);
            if (p == null) return NotFound();

            // MVP: only Notes is editable and saved here
            p.Notes = vm.Notes ?? "";
            await _db.SaveChangesAsync();

            TempData["Saved"] = "Notes updated.";
            return RedirectToAction(nameof(PatientDetails), new { id });
        }
    }
}

