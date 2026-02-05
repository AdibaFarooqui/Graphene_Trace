// Seeds users, patient profiles, and clinician-patient links. Idempotent (checks existence first).

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Software_Engineering_2328668.Models;

namespace Software_Engineering_2328668.Data
{
    /// <summary>
    /// Seeds demo data into MySQL if missing:
    /// - Users (admin/clinicians/patients)
    /// - PatientProfile (5 raw-data patients with thresholds in AU)
    /// - ClinicianPatient assignments (Matt->3, Skylar->2)
    /// </summary>
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Check DB connectivity (schema already created via SQL script)
            if (!await db.Database.CanConnectAsync())
                throw new InvalidOperationException("Cannot connect to GrapheneTraceDB. Check appsettings.json and MySQL (XAMPP) is running.");

            // ---------- 1) Seed USERS (if empty) ----------
            if (!await db.Users.AnyAsync())
            {
                static string Sha256(string s)
                {
                    using var sha = SHA256.Create();
                    var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
                    var sb = new StringBuilder(bytes.Length * 2);
                    foreach (var b in bytes) sb.Append(b.ToString("x2"));
                    return sb.ToString();
                }

                var now = DateTime.UtcNow;

                var seedUsers = new List<UserAccount>
                {
                    // Clinicians
                    new UserAccount { Username="mattrife", PasswordHash=Sha256("mattrife123"), Email="matt.rife@example.com", Role="clinician", IsActive=true, CreatedAt=now, UpdatedAt=now },
                    new UserAccount { Username="skylar",    PasswordHash=Sha256("skylar123"), Email="skylar.goodman@example.com", Role="clinician", IsActive=true, CreatedAt=now, UpdatedAt=now },

                    // Admin + demo patient
                    new UserAccount { Username="admin", PasswordHash=Sha256("admin123"), Email="admin@example.com", Role="admin", IsActive=true, CreatedAt=now, UpdatedAt=now },
                    new UserAccount { Username="john",  PasswordHash=Sha256("john123"),  Email="john@example.com", Role="patient", IsActive=true, CreatedAt=now, UpdatedAt=now },

                    // Five raw-data patients (password temp123)
                    new UserAccount { Username="p_javery",     PasswordHash=Sha256("temp123"), Email="john.avery@example.com",   Role="patient", IsActive=true, CreatedAt=now, UpdatedAt=now },
                    new UserAccount { Username="p_ecarter",    PasswordHash=Sha256("temp123"), Email="emily.carter@example.com", Role="patient", IsActive=true, CreatedAt=now, UpdatedAt=now },
                    new UserAccount { Username="p_mlee",       PasswordHash=Sha256("temp123"), Email="marcus.lee@example.com",   Role="patient", IsActive=true, CreatedAt=now, UpdatedAt=now },
                    new UserAccount { Username="p_shenderson", PasswordHash=Sha256("temp123"), Email="sarah.henderson@example.com", Role="patient", IsActive=true, CreatedAt=now, UpdatedAt=now },
                    new UserAccount { Username="p_dbrooks",    PasswordHash=Sha256("temp123"), Email="dylan.brooks@example.com", Role="patient", IsActive=true, CreatedAt=now, UpdatedAt=now }
                };

                await db.Users.AddRangeAsync(seedUsers);
                await db.SaveChangesAsync();
            }

            // Build a username -> user_id map we can use to seed relationships
            var idByUsername = await db.Users
                .AsNoTracking()
                .ToDictionaryAsync(u => u.Username, u => u.UserId);

            // Convenience getters (will throw if usernames missing)
            int CID(string uname) => idByUsername[uname];
            int PID(string uname) => idByUsername[uname];

            // ---------- 2) Seed PATIENT PROFILES (if missing) ----------
            // Helper: only add if profile doesn't exist for that patient_id
            async Task EnsureProfileAsync(string username, string sensoreId, string first, string last, string dob, decimal weightKg, int baseAu, int alertAu, string notes)
            {
                var patientId = PID(username);
                var exists = await db.PatientProfiles.AnyAsync(p => p.PatientId == patientId);
                if (exists) return;

                var profile = new PatientProfile
                {
                    PatientId = patientId,
                    SensoreId = sensoreId,
                    FirstName = first,
                    LastName = last,
                    Dob = DateTime.TryParse(dob, out var d) ? d : null,
                    WeightKg = weightKg,
                    BaseSeatingThresholdAu = baseAu,
                    AlertThresholdAu = alertAu,
                    Notes = notes
                };
                db.PatientProfiles.Add(profile);
                await db.SaveChangesAsync();
            }

            // The five raw-data patients

            await EnsureProfileAsync(
                username: "p_javery",
                sensoreId: "de0e9b2c",
                first: "John",
                last: "Avery",
                dob: "1982-03-14",
                weightKg: 78m,
                baseAu: 25,
                alertAu: 600,
                notes: "Patient reports mild discomfort during prolonged seating. Review cushion configuration next session."
            );
            await EnsureProfileAsync(
                username: "p_ecarter",
                sensoreId: "d13043b3",
                first: "Emily",
                last: "Carter",
                dob: "1995-11-22",
                weightKg: 62m,
                baseAu: 25,
                alertAu: 390,
                notes: "Pressure distribution stable over the last 3 visits. Monitoring asymmetry due to recent hip strain."
            );
            await EnsureProfileAsync(
                username: "p_mlee",
                sensoreId: "543d4676",
                first: "Marcus",
                last: "Lee",
                dob: "1978-07-09",
                weightKg: 84m,
                baseAu: 25,
                alertAu: 360,
                notes: "Elevated pressure after long transfers. Trial side bolsters for improved alignment."
            );
            await EnsureProfileAsync(
                username: "p_shenderson",
                sensoreId: "71e66ab3",
                first: "Sarah",
                last: "Henderson",
                dob: "2001-02-03",
                weightKg: 58m,
                baseAu: 25,
                alertAu: 480,
                notes: "New user to seating system; thresholds to be reassessed after adaptation period."
            );
            await EnsureProfileAsync(
                username: "p_dbrooks",
                sensoreId: "1c0fd777",
                first: "Dylan",
                last: "Brooks",
                dob: "1969-08-30",
                weightKg: 91m,
                baseAu: 25,
                alertAu: 300,
                notes: "High risk during extended wheelchair use. Scheduled pressure-relief every 20 minutes."
            );

            // ---------- 3) Seed CLINICIAN-PATIENT assignments (if missing) ----------
            // Insert clinician->patient mapping if missing (unique pair).
            async Task EnsureAssignmentAsync(string clinicianUsername, string patientUsername)
            {
                var clinicianId = CID(clinicianUsername);
                var patientId = PID(patientUsername);

                var exists = await db.ClinicianPatients
                    .AnyAsync(cp => cp.ClinicianId == clinicianId && cp.PatientId == patientId);
                if (exists) return;

                db.ClinicianPatients.Add(new ClinicianPatient
                {
                    ClinicianId = clinicianId,
                    PatientId = patientId,
                    AssignedAt = DateTime.UtcNow,
                    IsActive = true,
                    LastVisitedAt = null
                });
                await db.SaveChangesAsync();
            }

            // Matt: 3 patients
            await EnsureAssignmentAsync("mattrife", "p_javery");
            await EnsureAssignmentAsync("mattrife", "p_shenderson");
            await EnsureAssignmentAsync("mattrife", "p_dbrooks");

            // Skylar: 2 patients
            await EnsureAssignmentAsync("skylar", "p_ecarter");
            await EnsureAssignmentAsync("skylar", "p_mlee");
        }
    }
}

