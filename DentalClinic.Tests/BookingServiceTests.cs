using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DentalClinic.Tests;

public class BookingServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    // ─── GetAvailableSlotsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetSlots_ValidDoctor_ReturnsExpectedSlots()
    {
        using var db = CreateDb();
        SeedDoctor(db, userId: "doc-1", doctorId: 1,
            start: TimeSpan.FromHours(9), end: TimeSpan.FromHours(11), slotMinutes: 30);

        var svc   = new BookingService(db, NullLogger<BookingService>.Instance);
        var slots = await svc.GetAvailableSlotsAsync(1, new DateTime(2026, 6, 1));

        // 09:00, 09:30, 10:00, 10:30 → 4 slots
        Assert.Equal(4, slots.Count);
        Assert.Contains(new DateTime(2026, 6, 1, 9, 0, 0), slots);
        Assert.Contains(new DateTime(2026, 6, 1, 10, 30, 0), slots);
    }

    [Fact]
    public async Task GetSlots_DoctorNotFound_ReturnsEmpty()
    {
        using var db = CreateDb();
        var svc   = new BookingService(db, NullLogger<BookingService>.Instance);
        var slots = await svc.GetAvailableSlotsAsync(999, DateTime.Today);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task GetSlots_SlotDurationZero_ReturnsEmpty()
    {
        using var db = CreateDb();
        SeedDoctor(db, userId: "doc-2", doctorId: 2,
            start: TimeSpan.FromHours(9), end: TimeSpan.FromHours(17), slotMinutes: 0);

        var svc   = new BookingService(db, NullLogger<BookingService>.Instance);
        var slots = await svc.GetAvailableSlotsAsync(2, DateTime.Today);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task GetSlots_DoctorOnLeave_ReturnsEmpty()
    {
        using var db = CreateDb();
        SeedDoctor(db, userId: "doc-3", doctorId: 3,
            start: TimeSpan.FromHours(9), end: TimeSpan.FromHours(17), slotMinutes: 30);

        var leaveDate = new DateTime(2026, 6, 10);
        db.DoctorLeaves.Add(new DoctorLeave
        {
            DoctorId  = 3,
            StartDate = leaveDate,
            EndDate   = leaveDate
        });
        await db.SaveChangesAsync();

        var svc   = new BookingService(db, NullLogger<BookingService>.Instance);
        var slots = await svc.GetAvailableSlotsAsync(3, leaveDate);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task GetSlots_SomeBooked_ExcludesBookedTimes()
    {
        using var db = CreateDb();
        SeedDoctor(db, userId: "doc-4", doctorId: 4,
            start: TimeSpan.FromHours(9), end: TimeSpan.FromHours(11), slotMinutes: 30);

        var date = new DateTime(2026, 6, 15);
        db.Appointments.Add(new Appointment
        {
            CustomerId      = 1,
            DoctorId        = 4,
            ServiceId       = 1,
            AppointmentDate = date.AddHours(9),
            Status          = AppointmentStatus.Confirmed,
            Fee             = 200
        });
        await db.SaveChangesAsync();

        var svc   = new BookingService(db, NullLogger<BookingService>.Instance);
        var slots = await svc.GetAvailableSlotsAsync(4, date);

        Assert.DoesNotContain(date.AddHours(9), slots);
        Assert.Contains(date.AddHours(9).AddMinutes(30), slots);
    }

    [Fact]
    public async Task GetSlots_AllBooked_ReturnsEmpty()
    {
        using var db = CreateDb();
        SeedDoctor(db, userId: "doc-5", doctorId: 5,
            start: TimeSpan.FromHours(9), end: TimeSpan.FromHours(10), slotMinutes: 30);

        var date = new DateTime(2026, 6, 20);
        foreach (var offset in new[] { 0, 30 })
        {
            db.Appointments.Add(new Appointment
            {
                CustomerId      = 1,
                DoctorId        = 5,
                ServiceId       = 1,
                AppointmentDate = date.AddHours(9).AddMinutes(offset),
                Status          = AppointmentStatus.Confirmed,
                Fee             = 200
            });
        }
        await db.SaveChangesAsync();

        var svc   = new BookingService(db, NullLogger<BookingService>.Instance);
        var slots = await svc.GetAvailableSlotsAsync(5, date);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task GetSlots_CancelledAppointments_SlotRemainsAvailable()
    {
        using var db = CreateDb();
        SeedDoctor(db, userId: "doc-6", doctorId: 6,
            start: TimeSpan.FromHours(9), end: TimeSpan.FromHours(10), slotMinutes: 30);

        var date = new DateTime(2026, 6, 25);
        db.Appointments.Add(new Appointment
        {
            CustomerId      = 1,
            DoctorId        = 6,
            ServiceId       = 1,
            AppointmentDate = date.AddHours(9),
            Status          = AppointmentStatus.Cancelled,
            Fee             = 200
        });
        await db.SaveChangesAsync();

        var svc   = new BookingService(db, NullLogger<BookingService>.Instance);
        var slots = await svc.GetAvailableSlotsAsync(6, date);

        // Cancelled appointment must not block the slot
        Assert.Contains(date.AddHours(9), slots);
    }

    // ─── CreateAppointmentAsync ───────────────────────────────────────────────

    [Fact]
    public async Task CreateAppointment_SlotFree_ReturnsAppointment()
    {
        using var db = CreateDb();
        SeedDoctor(db, userId: "user-doc-1", doctorId: 1);
        SeedCustomer(db, userId: "user-cust-1", customerId: 1);
        SeedService(db, serviceId: 1);

        var svc   = new BookingService(db, NullLogger<BookingService>.Instance);
        var model = new BookingViewModel
        {
            DoctorId        = 1,
            ServiceId       = 1,
            AppointmentDate = DateTime.UtcNow.AddDays(1).Date.AddHours(9)
        };

        var result = await svc.CreateAppointmentAsync(model, "user-cust-1");

        Assert.NotNull(result);
        Assert.Equal(AppointmentStatus.Confirmed, result.Status);
    }

    [Fact]
    public async Task CreateAppointment_SlotTaken_ReturnsNull()
    {
        using var db = CreateDb();
        SeedDoctor(db, userId: "user-doc-1", doctorId: 1);
        SeedCustomer(db, userId: "user-cust-1", customerId: 1);
        SeedService(db, serviceId: 1);

        var bookedDate = DateTime.UtcNow.AddDays(1).Date.AddHours(9);
        db.Appointments.Add(new Appointment
        {
            CustomerId      = 1,
            DoctorId        = 1,
            ServiceId       = 1,
            AppointmentDate = bookedDate,
            Status          = AppointmentStatus.Confirmed,
            Fee             = 200
        });
        await db.SaveChangesAsync();

        var svc   = new BookingService(db, NullLogger<BookingService>.Instance);
        var model = new BookingViewModel
        {
            DoctorId        = 1,
            ServiceId       = 1,
            AppointmentDate = bookedDate
        };

        var result = await svc.CreateAppointmentAsync(model, "user-cust-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAppointment_DoctorOnLeave_ReturnsNull()
    {
        using var db = CreateDb();
        SeedDoctor(db, userId: "user-doc-1", doctorId: 1);
        SeedCustomer(db, userId: "user-cust-1", customerId: 1);
        SeedService(db, serviceId: 1);

        var leaveDate = DateTime.UtcNow.AddDays(1).Date;
        db.DoctorLeaves.Add(new DoctorLeave
        {
            DoctorId  = 1,
            StartDate = leaveDate,
            EndDate   = leaveDate
        });
        await db.SaveChangesAsync();

        var svc   = new BookingService(db, NullLogger<BookingService>.Instance);
        var model = new BookingViewModel
        {
            DoctorId        = 1,
            ServiceId       = 1,
            AppointmentDate = leaveDate.AddHours(9)
        };

        var result = await svc.CreateAppointmentAsync(model, "user-cust-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateAppointment_CustomerNotFound_ReturnsNull()
    {
        using var db = CreateDb();
        SeedService(db, serviceId: 1);

        var svc   = new BookingService(db, NullLogger<BookingService>.Instance);
        var model = new BookingViewModel
        {
            DoctorId        = 1,
            ServiceId       = 1,
            AppointmentDate = DateTime.UtcNow.AddDays(1)
        };

        var result = await svc.CreateAppointmentAsync(model, "nonexistent-user");

        Assert.Null(result);
    }

    // ─── CancelAppointmentAsync ───────────────────────────────────────────────

    [Fact]
    public async Task CancelAppointment_Owner_Succeeds()
    {
        using var db = CreateDb();
        var (_, apptId) = SeedAppointmentWithCustomer(db, ownerUserId: "user-cust-1");

        var svc    = new BookingService(db, NullLogger<BookingService>.Instance);
        var result = await svc.CancelAppointmentAsync(apptId, "user-cust-1", isSecretary: false);

        Assert.True(result);
        Assert.Equal(AppointmentStatus.Cancelled, db.Appointments.Find(apptId)!.Status);
    }

    [Fact]
    public async Task CancelAppointment_NotOwner_NotSecretary_ReturnsFalse()
    {
        using var db = CreateDb();
        var (_, apptId) = SeedAppointmentWithCustomer(db, ownerUserId: "user-cust-1");

        var svc    = new BookingService(db, NullLogger<BookingService>.Instance);
        var result = await svc.CancelAppointmentAsync(apptId, "different-user", isSecretary: false);

        Assert.False(result);
        Assert.NotEqual(AppointmentStatus.Cancelled, db.Appointments.Find(apptId)!.Status);
    }

    [Fact]
    public async Task CancelAppointment_Secretary_CanCancelAnyAppointment()
    {
        using var db = CreateDb();
        var (_, apptId) = SeedAppointmentWithCustomer(db, ownerUserId: "user-cust-1");

        var svc    = new BookingService(db, NullLogger<BookingService>.Instance);
        var result = await svc.CancelAppointmentAsync(apptId, "secretary-user-id", isSecretary: true);

        Assert.True(result);
        Assert.Equal(AppointmentStatus.Cancelled, db.Appointments.Find(apptId)!.Status);
    }

    [Fact]
    public async Task CancelAppointment_CompletedAppointment_ReturnsFalse()
    {
        using var db = CreateDb();
        var (_, apptId) = SeedAppointmentWithCustomer(db, ownerUserId: "user-cust-1",
            status: AppointmentStatus.Completed);

        var svc    = new BookingService(db, NullLogger<BookingService>.Instance);
        var result = await svc.CancelAppointmentAsync(apptId, "user-cust-1", isSecretary: false);

        Assert.False(result);
    }

    // ─── CompleteAppointmentAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CompleteAppointment_CorrectDoctor_Succeeds()
    {
        using var db = CreateDb();
        SeedDoctor(db, userId: "doc-user", doctorId: 10);
        SeedCustomer(db, userId: "cust-user", customerId: 5);
        SeedService(db, serviceId: 1);
        db.Appointments.Add(new Appointment
        {
            AppointmentId   = 99,
            DoctorId        = 10,
            CustomerId      = 5,
            ServiceId       = 1,
            AppointmentDate = DateTime.UtcNow.AddHours(-1),
            Status          = AppointmentStatus.Confirmed,
            Fee             = 300
        });
        await db.SaveChangesAsync();

        var svc    = new BookingService(db, NullLogger<BookingService>.Instance);
        var result = await svc.CompleteAppointmentAsync(99, doctorId: 10);

        Assert.True(result);
        Assert.Equal(AppointmentStatus.Completed, db.Appointments.Find(99)!.Status);
    }

    [Fact]
    public async Task CompleteAppointment_WrongDoctor_ReturnsFalse()
    {
        using var db = CreateDb();
        SeedDoctor(db, userId: "doc-user", doctorId: 10);
        SeedCustomer(db, userId: "cust-user", customerId: 5);
        SeedService(db, serviceId: 1);
        db.Appointments.Add(new Appointment
        {
            AppointmentId   = 99,
            DoctorId        = 10,
            CustomerId      = 5,
            ServiceId       = 1,
            AppointmentDate = DateTime.UtcNow.AddHours(-1),
            Status          = AppointmentStatus.Confirmed,
            Fee             = 300
        });
        await db.SaveChangesAsync();

        var svc    = new BookingService(db, NullLogger<BookingService>.Instance);
        var result = await svc.CompleteAppointmentAsync(99, doctorId: 99);

        Assert.False(result);
    }

    // ─── Seed helpers ─────────────────────────────────────────────────────────

    private static void SeedDoctor(
        AppDbContext db,
        string userId,
        int doctorId,
        TimeSpan? start       = null,
        TimeSpan? end         = null,
        int slotMinutes       = 30)
    {
        db.Users.Add(new ApplicationUser { Id = userId, UserName = userId, Email = $"{userId}@test.com", FullName = "Test Doctor" });
        db.Doctors.Add(new Doctor
        {
            DoctorId            = doctorId,
            UserId              = userId,
            Specialty           = "General",
            CommissionRate      = 0.70m,
            WorkingHoursStart   = start ?? TimeSpan.FromHours(9),
            WorkingHoursEnd     = end   ?? TimeSpan.FromHours(17),
            SlotDurationMinutes = slotMinutes
        });
        db.SaveChanges();
    }

    private static void SeedCustomer(AppDbContext db, string userId, int customerId)
    {
        db.Users.Add(new ApplicationUser { Id = userId, UserName = userId, Email = $"{userId}@test.com", FullName = "Test Patient" });
        db.Customers.Add(new Customer { CustomerId = customerId, UserId = userId });
        db.SaveChanges();
    }

    private static void SeedService(AppDbContext db, int serviceId)
    {
        db.Services.Add(new DentalClinic.Web.Models.Service
        {
            ServiceId       = serviceId,
            Name            = "Test Service",
            BaseFee         = 200,
            DurationMinutes = 30
        });
        db.SaveChanges();
    }

    private static (int customerId, int apptId) SeedAppointmentWithCustomer(
        AppDbContext db,
        string ownerUserId,
        AppointmentStatus status = AppointmentStatus.Confirmed)
    {
        db.Users.Add(new ApplicationUser { Id = ownerUserId, UserName = ownerUserId, Email = $"{ownerUserId}@test.com", FullName = "Patient" });
        db.Customers.Add(new Customer { CustomerId = 50, UserId = ownerUserId });
        db.Services.Add(new DentalClinic.Web.Models.Service { ServiceId = 9, Name = "S", BaseFee = 100, DurationMinutes = 30 });
        db.Appointments.Add(new Appointment
        {
            AppointmentId   = 200,
            CustomerId      = 50,
            DoctorId        = 1,
            ServiceId       = 9,
            AppointmentDate = DateTime.UtcNow.AddDays(1),
            Status          = status,
            Fee             = 100
        });
        db.SaveChanges();
        return (50, 200);
    }
}
