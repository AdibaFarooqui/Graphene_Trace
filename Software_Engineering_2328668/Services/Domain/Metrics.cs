namespace Software_Engineering_2328668.Services.Domain
{
    public static class Metrics
    {
        // Contact area in %: count of pixels >= threshold / 1024 * 100
        public static double ContactAreaPct(ReadOnlySpan<ushort> frame1024, int thresholdAu)
        {
            if (frame1024.Length != 1024) throw new ArgumentException("Expected 1024 pixels");
            int count = 0;
            for (int i = 0; i < 1024; i++)
                if (frame1024[i] >= thresholdAu) count++;
            return (count * 100.0) / 1024.0;
        }

        // PPI over a rolling 10s window: average of per-frame maxima over fps*10 frames
        public static double PpiRolling(ReadOnlySpan<int> frameMaxima, int fps)
        {
            if (fps <= 0) throw new ArgumentOutOfRangeException(nameof(fps));
            int window = Math.Max(1, fps * 10);
            int n = Math.Min(window, frameMaxima.Length);
            long sum = 0;
            for (int i = 0; i < n; i++) sum += frameMaxima[i];
            return n == 0 ? 0 : sum / (double)n;
        }

        // Alert if value >= threshold for at least minFrames consecutive frames
        public static bool DetectAlert(ReadOnlySpan<int> peaks, int thresholdAu, int minFrames)
        {
            int run = 0;
            for (int i = 0; i < peaks.Length; i++)
            {
                run = (peaks[i] >= thresholdAu) ? run + 1 : 0;
                if (run >= minFrames) return true;
            }
            return false;
        }
    }
}
