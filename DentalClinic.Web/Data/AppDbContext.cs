using DentalClinic.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Web.Data
{
    /// <summary>
    /// EF Core DbContext for the Dental Clinic system.
    /// Inherits from IdentityDbContext to integrate ASP.NET Core Identity tables.
    /// </summary>
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // ─── Domain DbSets ───────────────────────────────────────────────────────
        public DbSet<Doctor> Doctors { get; set; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<Service> Services { get; set; } = null!;
        public DbSet<Appointment> Appointments { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<DoctorLeave> DoctorLeaves { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ─── ApplicationUser → Doctor (1-to-1) ───────────────────────────────
            builder.Entity<Doctor>()
                .HasOne(d => d.User)
                .WithOne(u => u.DoctorProfile)
                .HasForeignKey<Doctor>(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ─── ApplicationUser → Customer (1-to-1) ─────────────────────────────
            builder.Entity<Customer>()
                .HasOne(c => c.User)
                .WithOne(u => u.CustomerProfile)
                .HasForeignKey<Customer>(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ─── Doctor → Appointments (1-to-N) ──────────────────────────────────
            builder.Entity<Appointment>()
                .HasOne(a => a.Doctor)
                .WithMany(d => d.Appointments)
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.Restrict); // prevent cascade delete

            // ─── Customer → Appointments (1-to-N) ────────────────────────────────
            builder.Entity<Appointment>()
                .HasOne(a => a.Customer)
                .WithMany(c => c.Appointments)
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // ─── Service → Appointments (1-to-N) ─────────────────────────────────
            builder.Entity<Appointment>()
                .HasOne(a => a.Service)
                .WithMany(s => s.Appointments)
                .HasForeignKey(a => a.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);

            // ─── BR-01: Unique constraint — no double-booking ─────────────────────
            // Prevents two appointments for the same doctor at the same date/time.
            builder.Entity<Appointment>()
                .HasIndex(a => new { a.DoctorId, a.AppointmentDate })
                .IsUnique()
                .HasDatabaseName("UX_Appointment_Doctor_DateTime");

            // ─── Appointment → Payment (1-to-1) ──────────────────────────────────
            builder.Entity<Payment>()
                .HasOne(p => p.Appointment)
                .WithOne(a => a.Payment)
                .HasForeignKey<Payment>(p => p.AppointmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ─── BR-03: One payment per appointment (unique index) ─────────────────
            builder.Entity<Payment>()
                .HasIndex(p => p.AppointmentId)
                .IsUnique()
                .HasDatabaseName("UX_Payment_AppointmentId");

            // ─── Customer → Notifications (1-to-N) ───────────────────────────────
            builder.Entity<Notification>()
                .HasOne(n => n.Customer)
                .WithMany(c => c.Notifications)
                .HasForeignKey(n => n.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // ─── Doctor → DoctorLeaves (1-to-N) ──────────────────────────────────
            builder.Entity<DoctorLeave>()
                .HasOne(l => l.Doctor)
                .WithMany(d => d.Leaves)
                .HasForeignKey(l => l.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);

            // ─── Decimal precision configurations ─────────────────────────────────
            builder.Entity<Doctor>()
                .Property(d => d.CommissionRate)
                .HasColumnType("decimal(5,4)");

            builder.Entity<Appointment>()
                .Property(a => a.Fee)
                .HasColumnType("decimal(10,2)");

            builder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(10,2)");

            builder.Entity<Service>()
                .Property(s => s.BaseFee)
                .HasColumnType("decimal(10,2)");

            // ─── Seed initial roles (handled via SeedData in Program.cs) ──────────
        }
    }
}
