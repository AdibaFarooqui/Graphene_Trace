using System.Linq;
using Software_Engineering_2328668.Services.Domain;
using Xunit;

namespace Software_Engineering_2328668.Tests
{
    public class MetricsTests
    {
        [Fact]
        public void ContactAreaPct_CountsPixelsAtOrAboveThreshold()
        {
            // Arrange: 512 pixels at 30 AU, 512 at 0 AU -> threshold 25 AU -> expect ~50%
            var frame = new ushort[1024];
            for (int i = 0; i < 512; i++) frame[i] = 30;
            for (int i = 512; i < 1024; i++) frame[i] = 0;

            // Act
            double pct = Metrics.ContactAreaPct(frame, thresholdAu: 25);

            // Assert
            Assert.InRange(pct, 49.9, 50.1);
        }

        [Fact]
        public void DetectAlert_True_WhenAboveThresholdForMinFrames()
        {
            // Arrange: Threshold 400 AU; 150 frames required; run of 160 frames >= 400
            var peaks = Enumerable.Repeat(0, 200).ToArray();
            for (int i = 20; i < 180; i++) peaks[i] = 420;

            // Act
            bool alert = Metrics.DetectAlert(peaks, thresholdAu: 400, minFrames: 150);

            // Assert
            Assert.True(alert);
        }
    }
}
