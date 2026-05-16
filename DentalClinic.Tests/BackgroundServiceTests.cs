using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using DentalClinic.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DentalClinic.Tests;

public class BackgroundServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IServiceProvider BuildProvider(AppDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IEmailService>(
            new EmailService(
                new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
                NullLogger<EmailService>.Instance));
        return services.BuildServiceProvider();
    }

    // ─── NotificationCleanupService ───────────────────────────────────────────

    [Fact]
    public async Task Cleanup_DeletesReadNotificationsOlderThan30Days()
    {
        using var db = CreateDb();

        // Old read notification (should be deleted)
        db.Notifications.Add(new Notification
        {
            CustomerId    = 1,
            Message       = "Old read",
            IsRead        = true,
            CreatedAt     = DateTime.UtcNow.AddDays(-31)
        });

        // Recent read notification (should remain)
        db.Notifications.Add(new Notification
        {
            CustomerId    = 1,
            Message       = "Recent read",
            IsRead        = true,
            CreatedAt     = DateTime.UtcNow.AddDays(-5)
        });

        // Old unread notification (should remain — not read yet)
        db.Notifications.Add(new Notification
        {
            CustomerId    = 1,
            Message       = "Old unread",
            IsRead        = false,
            CreatedAt     = DateTime.UtcNow.AddDays(-40)
        });

        await db.SaveChangesAsync();

        var factory = new InMemoryServiceScopeFactory(BuildProvider(db));
        var service = new NotificationCleanupService(factory, NullLogger<NotificationCleanupService>.Instance);

        await service.RunOnceForTestAsync();

        var remaining = await db.Notifications.ToListAsync();
        Assert.Equal(2, remaining.Count);
        Assert.DoesNotContain(remaining, n => n.Message == "Old read");
        Assert.Contains(remaining, n => n.Message == "Recent read");
        Assert.Contains(remaining, n => n.Message == "Old unread");
    }

    [Fact]
    public async Task Cleanup_NothingToDelete_TableUnchanged()
    {
        using var db = CreateDb();

        db.Notifications.Add(new Notification
        {
            CustomerId = 1,
            Message    = "Keep me",
            IsRead     = false,
            CreatedAt  = DateTime.UtcNow.AddDays(-60)
        });
        await db.SaveChangesAsync();

        var factory = new InMemoryServiceScopeFactory(BuildProvider(db));
        var service = new NotificationCleanupService(factory, NullLogger<NotificationCleanupService>.Instance);

        await service.RunOnceForTestAsync();

        Assert.Equal(1, await db.Notifications.CountAsync());
    }

    // ─── ReminderBackgroundService ────────────────────────────────────────────

    [Fact]
    public async Task Reminder_CreatesNotificationForUpcomingConfirmedAppointment()
    {
        using var db = CreateDb();
        SeedAppointment(db, appointmentId: 1, customerId: 1,
            date: DateTime.UtcNow.AddHours(12),
            status: AppointmentStatus.Confirmed,
            reminderSent: false);

        var factory = new InMemoryServiceScopeFactory(BuildProvider(db));
        var service = new ReminderBackgroundService(factory, NullLogger<ReminderBackgroundService>.Instance);

        await service.RunOnceForTestAsync();

        var notification = await db.Notifications.FirstOrDefaultAsync();
        Assert.NotNull(notification);
        Assert.Equal(1, notification.CustomerId);

        var appointment = await db.Appointments.FindAsync(1);
        Assert.True(appointment!.ReminderSent);
    }

    [Fact]
    public async Task Reminder_SkipsAlreadyRemindedAppointment()
    {
        using var db = CreateDb();
        SeedAppointment(db, appointmentId: 2, customerId: 1,
            date: DateTime.UtcNow.AddHours(6),
            status: AppointmentStatus.Confirmed,
            reminderSent: true);

        var factory = new InMemoryServiceScopeFactory(BuildProvider(db));
        var service = new ReminderBackgroundService(factory, NullLogger<ReminderBackgroundService>.Instance);

        await service.RunOnceForTestAsync();

        Assert.Equal(0, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Reminder_SkipsCancelledAppointment()
    {
        using var db = CreateDb();
        SeedAppointment(db, appointmentId: 3, customerId: 1,
            date: DateTime.UtcNow.AddHours(10),
            status: AppointmentStatus.Cancelled,
            reminderSent: false);

        var factory = new InMemoryServiceScopeFactory(BuildProvider(db));
        var service = new ReminderBackgroundService(factory, NullLogger<ReminderBackgroundService>.Instance);

        await service.RunOnceForTestAsync();

        Assert.Equal(0, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Reminder_SkipsAppointmentBeyond24Hours()
    {
        using var db = CreateDb();
        SeedAppointment(db, appointmentId: 4, customerId: 1,
            date: DateTime.UtcNow.AddHours(25),
            status: AppointmentStatus.Confirmed,
            reminderSent: false);

        var factory = new InMemoryServiceScopeFactory(BuildProvider(db));
        var service = new ReminderBackgroundService(factory, NullLogger<ReminderBackgroundService>.Instance);

        await service.RunOnceForTestAsync();

        Assert.Equal(0, await db.Notifications.CountAsync());
        var appt = await db.Appointments.FindAsync(4);
        Assert.False(appt!.ReminderSent);
    }

    // ─── Seed helpers ─────────────────────────────────────────────────────────

    private static void SeedAppointment(
        AppDbContext db,
        int appointmentId,
        int customerId,
        DateTime date,
        AppointmentStatus status,
        bool reminderSent)
    {
        // Patient
        db.Users.Add(new ApplicationUser
        {
            Id       = $"user-{customerId}",
            UserName = $"user-{customerId}",
            Email    = $"user{customerId}@test.com",
            FullName = "Test Patient"
        });
        db.Customers.Add(new Customer { CustomerId = customerId, UserId = $"user-{customerId}" });

        // Doctor (required by ReminderBackgroundService.Include)
        var docUserId = $"doc-user-{appointmentId}";
        db.Users.Add(new ApplicationUser
        {
            Id       = docUserId,
            UserName = docUserId,
            Email    = $"doctor{appointmentId}@test.com",
            FullName = "Test Doctor"
        });
        db.Doctors.Add(new Doctor
        {
            DoctorId            = appointmentId,
            UserId              = docUserId,
            Specialty           = "General",
            CommissionRate      = 0.70m,
            WorkingHoursStart   = TimeSpan.FromHours(9),
            WorkingHoursEnd     = TimeSpan.FromHours(17),
            SlotDurationMinutes = 30
        });

        // Service
        db.Services.Add(new DentalClinic.Web.Models.Service
        {
            ServiceId       = appointmentId,
            Name            = "Test Service",
            BaseFee         = 200,
            DurationMinutes = 30
        });

        db.Appointments.Add(new Appointment
        {
            AppointmentId   = appointmentId,
            CustomerId      = customerId,
            DoctorId        = appointmentId,
            ServiceId       = appointmentId,
            AppointmentDate = date,
            Status          = status,
            ReminderSent    = reminderSent,
            Fee             = 200
        });
        db.SaveChanges();
    }
}

// ─── Test helper: IServiceScopeFactory backed by an existing IServiceProvider ─

internal sealed class InMemoryServiceScopeFactory : IServiceScopeFactory
{
    private readonly IServiceProvider _provider;
    public InMemoryServiceScopeFactory(IServiceProvider provider) => _provider = provider;
    public IServiceScope CreateScope() => new InMemoryServiceScope(_provider);
}

internal sealed class InMemoryServiceScope : IServiceScope
{
    public IServiceProvider ServiceProvider { get; }
    public InMemoryServiceScope(IServiceProvider provider) => ServiceProvider = provider;
    public void Dispose() { }
}
