using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Software_Engineering_2328668.Data;
using Software_Engineering_2328668.Models;

namespace Software_Engineering_2328668.Services
{
    /// <summary>
    /// Serves chunked 32x32 AU frames for a dataset.
    /// Lazily builds a binary cache (ushort[1024] per frame) from the original CSV:
    ///   App_Data/cache/{dataset_code}.bin
    /// </summary>
    public class FrameDataService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<FrameDataService> _logger;
        private readonly IWebHostEnvironment _env;

        public FrameDataService(AppDbContext db, ILogger<FrameDataService> logger, IWebHostEnvironment env)
        {
            _db = db;
            _logger = logger;
            _env = env;
        }

        private string CsvFolder()
            => Path.Combine(_env.ContentRootPath, "App_Data", "csv");

        private string CacheFolder()
        {
            var p = Path.Combine(_env.ContentRootPath, "App_Data", "cache");
            if (!Directory.Exists(p)) Directory.CreateDirectory(p);
            return p;
        }

        private static string DatasetCacheName(Dataset ds) => $"{ds.DatasetCode}.bin";

        /// <summary>
        /// Ensure a dataset has a built cache file. Returns path + (frames,width,height).
        /// </summary>
        public async Task<(string cachePath, int frames, int width, int height)> EnsureCacheAsync(int datasetId, CancellationToken ct = default)
        {
            var ds = await _db.Datasets.AsNoTracking().FirstOrDefaultAsync(d => d.DatasetId == datasetId, ct)
                ?? throw new InvalidOperationException($"Dataset {datasetId} not found.");

            var cachePath = Path.Combine(CacheFolder(), DatasetCacheName(ds));
            if (File.Exists(cachePath))
            {
                // Quick sanity check: expected size = frames * 1024 * sizeof(ushort)
                long expected = (long)ds.FramesCount * 1024L * 2L;
                var fi = new FileInfo(cachePath);
                if (fi.Length == expected) return (cachePath, ds.FramesCount, ds.Width, ds.Height);
                _logger.LogWarning("Cache size mismatch for {cache}; rebuilding.", cachePath);
                File.Delete(cachePath);
            }

            // Build from original CSV
            var csvPath = Path.Combine(CsvFolder(), ds.OriginalName);
            if (!File.Exists(csvPath))
                throw new FileNotFoundException($"Original CSV not found: {csvPath}");

            const int WIDTH = 32;
            const int HEIGHT = 32;

            using var reader = new StreamReader(csvPath);
            // We'll parse as simple CSV with Split for speed & no headers
            // Each frame = 32 lines * 32 integers
            using var fs = new FileStream(cachePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var bw = new BinaryWriter(fs);

            var rowsBuffer = new int[HEIGHT][];
            int rowCount = 0;
            int framesWritten = 0;

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < WIDTH) continue;

                var row = new int[WIDTH];
                for (int c = 0; c < WIDTH; c++)
                {
                    // parse tolerant
                    if (!int.TryParse(parts[c].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                        v = 0;
                    row[c] = v;
                }

                rowsBuffer[rowCount++] = row;

                if (rowCount == HEIGHT)
                {
                    // Write one frame as 1024 ushorts row-major
                    for (int r = 0; r < HEIGHT; r++)
                    {
                        var arr = rowsBuffer[r];
                        for (int c = 0; c < WIDTH; c++)
                        {
                            ushort u = (ushort)Math.Clamp(arr[c], 0, 65535);
                            bw.Write(u);
                        }
                    }
                    framesWritten++;
                    rowCount = 0;
                }
            }

            _logger.LogInformation("Built cache {cache} with {frames} frames.", cachePath, framesWritten);
            return (cachePath, framesWritten, WIDTH, HEIGHT);
        }

        /// <summary>
        /// Reads frames as flattened ushort array for [offset, offset+count).
        /// Returns ushort[count * 1024].
        /// </summary>
        public async Task<ushort[]> ReadFramesAsync(int datasetId, int offset, int count, CancellationToken ct = default)
        {
            var (cache, frames, width, height) = await EnsureCacheAsync(datasetId, ct);
            int total = frames;

            if (offset < 0) offset = 0;
            if (count < 0) count = 0;
            if (offset > total) offset = total;
            if (offset + count > total) count = total - offset;

            int pixelsPerFrame = width * height; // 1024
            long byteOffset = (long)offset * pixelsPerFrame * 2L;
            int bytesToRead = count * pixelsPerFrame * 2;

            var data = new ushort[count * pixelsPerFrame];
            if (count == 0) return data;

            using var fs = new FileStream(cache, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
            fs.Seek(byteOffset, SeekOrigin.Begin);

            var buf = new byte[bytesToRead];
            int read = 0;
            while (read < bytesToRead)
            {
                int r = await fs.ReadAsync(buf.AsMemory(read, bytesToRead - read), ct);
                if (r == 0) break;
                read += r;
            }

            // Convert little-endian bytes -> ushorts
            Buffer.BlockCopy(buf, 0, data, 0, read);
            return data;
        }
    }
}
