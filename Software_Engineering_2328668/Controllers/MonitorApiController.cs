using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Software_Engineering_2328668.Data;
using Software_Engineering_2328668.Services;

namespace Software_Engineering_2328668.Controllers
{
    [ApiController]
    [Route("api/monitor")]
    [Authorize(Roles = "clinician")]
    public class MonitorApiController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly FrameDataService _frames;
        public MonitorApiController(AppDbContext db, FrameDataService frames)
        {
            _db = db;
            _frames = frames;
        }

        private int CurrentClinicianId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idStr, out var clinicianId)) throw new InvalidOperationException("No clinician id.");
            return clinicianId;
        }

        private async Task<bool> IsAssignedAsync(int clinicianId, int patientId)
        {
            return await _db.ClinicianPatients.AnyAsync(cp => cp.ClinicianId == clinicianId && cp.PatientId == patientId && cp.IsActive);
        }

        /// <summary>
        /// Dates available for the patient (yyyy-MM-dd). (kept for completeness)
        /// </summary>
        [HttpGet("dates")]
        public async Task<IActionResult> GetDates([FromQuery] int pid)
        {
            var clinicianId = CurrentClinicianId();
            if (!await IsAssignedAsync(clinicianId, pid)) return Forbid();

            var dates = await _db.Datasets.AsNoTracking()
                .Where(d => d.PatientId == pid && d.FileDate != null)
                .OrderBy(d => d.FileDate)
                .Select(d => d.FileDate!.Value.ToString("yyyy-MM-dd"))
                .Distinct()
                .ToListAsync();

            return Ok(dates);
        }

        /// <summary>
        /// Meta for a specific patient's dataset by date.
        /// </summary>
        [HttpGet("meta")]
        public async Task<IActionResult> GetMeta([FromQuery] int pid, [FromQuery] string date)
        {
            var clinicianId = CurrentClinicianId();
            if (!await IsAssignedAsync(clinicianId, pid)) return Forbid();

            if (!DateTime.TryParse(date, out var dt)) return BadRequest("Invalid date.");
            dt = dt.Date;

            var ds = await _db.Datasets.AsNoTracking()
                .Where(d => d.PatientId == pid && d.FileDate != null && d.FileDate.Value.Date == dt)
                .OrderBy(d => d.DatasetId)
                .FirstOrDefaultAsync();

            if (ds == null) return NotFound("Dataset not found for date.");

            var meta = new
            {
                datasetId = ds.DatasetId,
                fps = ds.Fps,
                frames = ds.FramesCount,
                durationSec = ds.DurationSeconds,
                width = ds.Width,
                height = ds.Height,
                startUtc = ds.StartTimeUtc,
                minAu = ds.MinAuDataset,
                maxAu = ds.MaxAuDataset
            };
            return Ok(meta);
        }

        /// <summary>
        /// Returns frames as int[][] (each length=1024) for [offset, offset+count).
        /// </summary>
        [HttpGet("frames")]
        public async Task<IActionResult> GetFrames([FromQuery] int datasetId, [FromQuery] int offset, [FromQuery] int count)
        {
            if (count <= 0 || count > 1000) count = Math.Clamp(count, 1, 1000);

            // Security: ensure the current clinician is assigned to the dataset's patient
            var ds = await _db.Datasets.AsNoTracking().FirstOrDefaultAsync(d => d.DatasetId == datasetId);
            if (ds == null) return NotFound("Dataset not found.");

            var clinicianId = CurrentClinicianId();
            if (!await IsAssignedAsync(clinicianId, ds.PatientId)) return Forbid();

            var data = await _frames.ReadFramesAsync(datasetId, offset, count);
            int width = ds.Width, height = ds.Height;
            int pixels = width * height;

            var frames = new int[count][];
            for (int i = 0; i < count; i++)
            {
                var arr = new int[pixels];
                // copy and widen
                for (int p = 0; p < pixels; p++)
                    arr[p] = data[i * pixels + p];
                frames[i] = arr;
            }
            return Ok(new { frames, width, height });
        }

        /// <summary>
        /// Returns per-frame metrics for [offset, offset+count) ordered by frame_index.
        /// </summary>
        [HttpGet("metrics")]
        public async Task<IActionResult> GetMetrics([FromQuery] int datasetId, [FromQuery] int offset, [FromQuery] int count)
        {
            if (count <= 0 || count > 2000) count = Math.Clamp(count, 1, 2000);

            var ds = await _db.Datasets.AsNoTracking().FirstOrDefaultAsync(d => d.DatasetId == datasetId);
            if (ds == null) return NotFound("Dataset not found.");

            var clinicianId = CurrentClinicianId();
            if (!await IsAssignedAsync(clinicianId, ds.PatientId)) return Forbid();

            var q = from f in _db.Frames.AsNoTracking()
                    where f.DatasetId == datasetId && f.FrameIndex >= offset && f.FrameIndex < offset + count
                    join m in _db.FrameMetrics.AsNoTracking() on f.FrameId equals m.FrameId
                    orderby f.FrameIndex
                    select new
                    {
                        i = f.FrameIndex,
                        peak = m.PeakPressureAu,
                        avg = m.AvgPressureAu,
                        contactPct = m.ContactAreaPct,
                        cov = m.CovPercent,
                        ppi = m.PpiAu10s
                    };

            var list = await q.ToListAsync();
            return Ok(list);
        }
    }
}
