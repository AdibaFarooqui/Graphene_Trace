using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Software_Engineering_2328668.Data;
using Software_Engineering_2328668.Models;

namespace Software_Engineering_2328668.Services
{
    /// <summary>
    /// Reads 32x32 AU CSV files from App_Data/csv and populates datasets, frames, frame_metrics, and alerts.
    /// File name format: {sensore_id}_{yyyyMMdd}.csv
    /// Assumptions: ~15 fps; start=15:00 local time on file_date stored as UTC.
    /// </summary>
    public class CsvIngestionService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<CsvIngestionService> _logger;
        private readonly IWebHostEnvironment _env;

        public CsvIngestionService(AppDbContext db, ILogger<CsvIngestionService> logger, IWebHostEnvironment env)
        {
            _db = db;
            _logger = logger;
            _env = env;
        }

        public async Task<(int datasets, int frames, int alerts)> RunForAllCsvAsync(CancellationToken ct = default)
        {
            var root = _env.ContentRootPath; // project root at runtime
            var folder = Path.Combine(root, "App_Data", "csv");
            if (!Directory.Exists(folder))
            {
                _logger.LogWarning("CSV folder not found: {folder}", folder);
                return (0, 0, 0);
            }

            var csvFiles = Directory.GetFiles(folder, "*.csv", SearchOption.TopDirectoryOnly);
            int dsCount = 0, frameCount = 0, alertCount = 0;

            foreach (var path in csvFiles)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var result = await IngestOneAsync(path, ct);
                    dsCount += result.datasets;
                    frameCount += result.frames;
                    alertCount += result.alerts;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to ingest {file}", Path.GetFileName(path));
                }
            }

            return (dsCount, frameCount, alertCount);
        }

        private async Task<(int datasets, int frames, int alerts)> IngestOneAsync(string filePath, CancellationToken ct)
        {
            string fileName = Path.GetFileName(filePath);
            string name = Path.GetFileNameWithoutExtension(filePath);

            // Parse {sensoreId}_{yyyyMMdd}
            var parts = name.Split('_', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                _logger.LogWarning("Skipping file with unexpected name: {fn}", fileName);
                return (0, 0, 0);
            }

            string sensoreId = parts[0];
            if (!DateTime.TryParseExact(parts[1], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate))
            {
                _logger.LogWarning("Skipping file with unparseable date: {fn}", fileName);
                return (0, 0, 0);
            }

            // Find patient by sensore_id
            var profile = await _db.PatientProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.SensoreId == sensoreId, ct);
            if (profile == null)
            {
                _logger.LogWarning("No patient profile for sensore {sid}; skipping {fn}", sensoreId, fileName);
                return (0, 0, 0);
            }

            // Avoid duplicate dataset per patient/dataset_code
            string datasetCode = $"{sensoreId}_{fileDate:yyyyMMdd}";
            bool exists = await _db.Datasets.AsNoTracking()
                .AnyAsync(d => d.PatientId == profile.PatientId && d.DatasetCode == datasetCode, ct);
            if (exists)
            {
                _logger.LogInformation("Dataset already ingested: {code}", datasetCode);
                return (0, 0, 0);
            }

            const int WIDTH = 32;
            const int HEIGHT = 32;
            const decimal FPS = 15.0m;                         // assumption
            int thresholdAu = profile.BaseSeatingThresholdAu ?? 25;

            // 15:00 local ⇒ UTC
            var localStart = new DateTime(fileDate.Year, fileDate.Month, fileDate.Day, 15, 0, 0, DateTimeKind.Unspecified);
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, TimeZoneInfo.Local);

            // Parse CSV rows group-of-32 to form frames
            var framesAgg = new List<(decimal min, decimal max, decimal mean, decimal std, int contactPx)>(capacity: 4096);
            var peaks = new List<decimal>(capacity: 4096);
            decimal datasetMin = decimal.MaxValue, datasetMax = decimal.MinValue;

            using var reader = new StreamReader(filePath);
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,    // files have no header row
                TrimOptions = TrimOptions.Trim,
                BadDataFound = null,        // ignore stray bad fields
                MissingFieldFound = null    // ignore missing fields (defensive)
            };
            using var csv = new CsvReader(reader, csvConfig);

            // To be read row-by-row; each row must have 32 ints
            var rowsBuffer = new List<int[]>(HEIGHT);
            while (await csv.ReadAsync())
            {
                ct.ThrowIfCancellationRequested();

                // Read 32 columns as ints
                var row = new int[WIDTH];
                for (int c = 0; c < WIDTH; c++)
                {
                    // CsvHelper: get field value by index
                    row[c] = csv.GetField<int>(c);
                }
                rowsBuffer.Add(row);

                if (rowsBuffer.Count == HEIGHT)
                {
                    // Build a frame and compute metrics
                    long sum = 0;
                    long sumSq = 0;
                    int contactPx = 0;
                    int fMin = int.MaxValue, fMax = int.MinValue;

                    // iterate 32x32 pixels
                    for (int r = 0; r < HEIGHT; r++)
                    {
                        var rarr = rowsBuffer[r];
                        for (int c = 0; c < WIDTH; c++)
                        {
                            int v = rarr[c];
                            if (v < fMin) fMin = v;
                            if (v > fMax) fMax = v;

                            sum += v;
                            sumSq += (long)v * v;

                            if (v >= thresholdAu) contactPx++;
                        }
                    }

                    // frame stats
                    decimal mean = sum / 1024m;
                    // population stddev
                    decimal variance = (sumSq / 1024m) - (mean * mean);
                    if (variance < 0) variance = 0; // numerical guard
                    decimal std = (decimal)Math.Sqrt((double)variance);
                    decimal cov = mean > 0 ? (std / mean) * 100m : 0m;

                    framesAgg.Add((fMin, fMax, mean, std, contactPx));
                    peaks.Add(fMax);

                    // dataset min/max
                    if (fMin < datasetMin) datasetMin = fMin;
                    if (fMax > datasetMax) datasetMax = fMax;

                    rowsBuffer.Clear();
                }
            }

            int framesCount = framesAgg.Count;
            if (framesCount == 0)
            {
                _logger.LogWarning("No frames parsed from {fn}", fileName);
                return (0, 0, 0);
            }

            int durationS = (int)Math.Round(framesCount / (double)FPS);

            // Compute PPI-10s (rolling avg of frame peaks over ~10 seconds)
            int window = Math.Max(1, (int)Math.Round(10m * FPS)); // ~150 frames
            var ppi = new decimal[framesCount];
            decimal rollingSum = 0;
            var q = new Queue<decimal>(window);

            for (int i = 0; i < framesCount; i++)
            {
                var pk = peaks[i];
                q.Enqueue(pk);
                rollingSum += pk;
                if (q.Count > window) rollingSum -= q.Dequeue();
                ppi[i] = rollingSum / q.Count;
            }

            // Insert Dataset row first
            var ds = new Dataset
            {
                PatientId = profile.PatientId,
                SensoreId = sensoreId,
                DatasetCode = datasetCode,
                OriginalName = fileName,
                FileDate = fileDate,
                StartTimeUtc = startUtc,
                StartTimeSource = "filename_date_15h",
                DurationSeconds = durationS,
                FramesCount = framesCount,
                Fps = FPS,
                Width = WIDTH,
                Height = HEIGHT,
                MinAuDataset = datasetMin == decimal.MaxValue ? null : datasetMin,
                MaxAuDataset = datasetMax == decimal.MinValue ? null : datasetMax,
                P01AuDataset = null,  
                P99AuDataset = null,  
                StoragePath = null,
                Checksum = ComputeSha256File(filePath),
                IngestedAt = DateTime.UtcNow
            };

            _db.Datasets.Add(ds);
            await _db.SaveChangesAsync(ct); // get ds.DatasetId

            // Build Frame entities
            var frames = new List<Frame>(framesCount);
            for (int i = 0; i < framesCount; i++)
            {
                var tsUtc = startUtc.AddSeconds((double)i / (double)FPS);
                var a = framesAgg[i];

                frames.Add(new Frame
                {
                    DatasetId = ds.DatasetId,
                    FrameIndex = i,
                    TsUtc = tsUtc,
                    MinAu = a.min,
                    MaxAu = a.max,
                    MeanAu = a.mean,
                    StdAu = a.std,
                    MatrixRef = null
                });
            }

            _db.Frames.AddRange(frames);
            await _db.SaveChangesAsync(ct); // assigns FrameId to each

            // Build FrameMetrics entities aligned with frames by index
            var metrics = new List<FrameMetrics>(framesCount);
            for (int i = 0; i < framesCount; i++)
            {
                var a = framesAgg[i];
                var contactPct = (decimal)a.contactPx / 1024m * 100m;

                metrics.Add(new FrameMetrics
                {
                    FrameId = frames[i].FrameId,
                    PeakPressureAu = a.max,
                    AvgPressureAu = a.mean,
                    ContactAreaPx = a.contactPx,
                    ContactAreaPct = Math.Round(contactPct, 2),
                    CovPercent = Math.Round(a.mean > 0 ? (a.std / a.mean * 100m) : 0m, 2),
                    PpiAu10s = Math.Round(ppi[i], 3)
                });
            }

            _db.FrameMetrics.AddRange(metrics);
            await _db.SaveChangesAsync(ct);

            // Compute Alerts: PPI >= alert_threshold for >= 10 s
            int alertThreshold = profile.AlertThresholdAu ?? 999999;
            int minLen = window; // same as 10s (~150 frames)

            int runStart = -1;
            for (int i = 0; i < framesCount; i++)
            {
                bool above = ppi[i] >= alertThreshold;
                if (above && runStart < 0) runStart = i;

                bool endRun = (!above && runStart >= 0);
                bool lastFrame = (i == framesCount - 1);
                if (lastFrame && above) endRun = true;

                if (endRun)
                {
                    int runEnd = above ? i : i - 1;
                    int len = runEnd - runStart + 1;
                    if (len >= minLen)
                    {
                        var triggerTs = startUtc.AddSeconds(runStart / (double)FPS);
                        var alert = new Alert
                        {
                            PatientId = profile.PatientId,
                            DatasetId = ds.DatasetId,
                            TriggeredTsUtc = triggerTs,
                            StartFrameIndex = runStart,
                            EndFrameIndex = runEnd,
                            ThresholdAu = alertThreshold,
                            AboveForSeconds = (int)Math.Round(len / (double)FPS),
                            Severity = "high",
                            CreatedAt = DateTime.UtcNow
                        };
                        _db.Alerts.Add(alert);
                    }
                    runStart = -1;
                }
            }

            int createdAlerts = await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Ingested {frames} frames from {fn} for patient {pid}", framesCount, fileName, profile.PatientId);

            return (1, framesCount, createdAlerts);
        }

        private static string ComputeSha256File(string path)
        {
            using var sha = SHA256.Create();
            using var s = File.OpenRead(path);
            var hash = sha.ComputeHash(s);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
