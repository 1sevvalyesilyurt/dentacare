using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

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

    // ─── CreateAppointmentAsync ───────────────────────────────────────────────

    [Fact]
    public async Task CreateAppointment_SlotFree_ReturnsAppointment()
    {
        using var db = CreateDb();
        SeedDoctor(db, userId: "user-doc-1", doctorId: 1);
        SeedCustomer(db, userId: "user-cust-1", customerId: 1);
        SeedService(db, serviceId: 1);

        var svc   = new BookingService(db);
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

        var svc   = new BookingService(db);
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

        var svc   = new BookingService(db);
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

        var svc   = new BookingService(db);
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

        var svc    = new BookingService(db);
        var result = await svc.CancelAppointmentAsync(apptId, "user-cust-1", isSecretary: false);

        Assert.True(result);
        Assert.Equal(AppointmentStatus.Cancelled, db.Appointments.Find(apptId)!.Status);
    }

    [Fact]
    public async Task CancelAppointment_NotOwner_NotSecretary_ReturnsFalse()
    {
        using var db = CreateDb();
        var (_, apptId) = SeedAppointmentWithCustomer(db, ownerUserId: "user-cust-1");

        var svc    = new BookingService(db);
        var result = await svc.CancelAppointmentAsync(apptId, "different-user", isSecretary: false);

        Assert.False(result);
        Assert.NotEqual(AppointmentStatus.Cancelled, db.Appointments.Find(apptId)!.Status);
    }

    [Fact]
    public async Task CancelAppointment_Secretary_CanCancelAnyAppointment()
    {
        using var db = CreateDb();
        var (_, apptId) = SeedAppointmentWithCustomer(db, ownerUserId: "user-cust-1");

        var svc    = new BookingService(db);
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

        var svc    = new BookingService(db);
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

        var svc    = new BookingService(db);
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

        var svc    = new BookingService(db);
        var result = await svc.CompleteAppointmentAsync(99, doctorId: 99);

        Assert.False(result);
    }

    // ─── Seed helpers ─────────────────────────────────────────────────────────

    private static void SeedDoctor(AppDbContext db, string userId, int doctorId)
    {
        db.Users.Add(new ApplicationUser { Id = userId, UserName = userId, Email = $"{userId}@test.com", FullName = "Test Doctor" });
        db.Doctors.Add(new Doctor
        {
            DoctorId            = doctorId,
            UserId              = userId,
            Specialty           = "General",
            CommissionRate      = 0.70m,
            WorkingHoursStart   = TimeSpan.FromHours(9),
            WorkingHoursEnd     = TimeSpan.FromHours(17),
            SlotDurationMinutes = 30
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
