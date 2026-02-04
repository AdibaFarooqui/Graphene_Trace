using Microsoft.EntityFrameworkCore;
using Software_Engineering_2328668.Models;

namespace Software_Engineering_2328668.Data
{
    /// <summary>
    /// EF Core DB context. Includes Users, Profiles, Assignments, Datasets, Frames, Metrics, Alerts.
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Auth / identity (from Phase A)
        public DbSet<UserAccount> Users => Set<UserAccount>();
        public DbSet<PatientProfile> PatientProfiles => Set<PatientProfile>();
        public DbSet<ClinicianPatient> ClinicianPatients => Set<ClinicianPatient>();

        // Monitor data
        // Tables backing the Patient Monitor (datasets/frames/metrics) and notifications (alerts).
        public DbSet<Dataset> Datasets => Set<Dataset>();
        public DbSet<Frame> Frames => Set<Frame>();
        public DbSet<FrameMetrics> FrameMetrics => Set<FrameMetrics>();
        public DbSet<Alert> Alerts => Set<Alert>();
    }
}


