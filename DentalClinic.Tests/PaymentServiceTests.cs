using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Tests;

public class PaymentServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task RecordPayment_ValidCompletedAppointment_ReturnsInvoiceNumber()
    {
        using var db = CreateDb();
        SeedCompletedAppointment(db, appointmentId: 1, alreadyPaid: false);

        var svc    = new PaymentService(db);
        var model  = new PaymentRecordViewModel { AppointmentId = 1, Amount = 500, PaymentMethod = PaymentMethod.Cash };
        var result = await svc.RecordPaymentAsync(model, "secretary-id");

        Assert.NotNull(result);
        Assert.StartsWith("INV-", result);
    }

    [Fact]
    public async Task RecordPayment_AlreadyPaid_ReturnsNull()
    {
        using var db = CreateDb();
        SeedCompletedAppointment(db, appointmentId: 2, alreadyPaid: true);

        var svc    = new PaymentService(db);
        var model  = new PaymentRecordViewModel { AppointmentId = 2, Amount = 500, PaymentMethod = PaymentMethod.Cash };
        var result = await svc.RecordPaymentAsync(model, "secretary-id");

        Assert.Null(result);
    }

    [Fact]
    public async Task RecordPayment_NotCompletedStatus_ReturnsNull()
    {
        using var db = CreateDb();
        db.Appointments.Add(new Appointment
        {
            AppointmentId   = 3,
            CustomerId      = 1,
            DoctorId        = 1,
            ServiceId       = 1,
            AppointmentDate = DateTime.UtcNow.AddDays(1),
            Status          = AppointmentStatus.Confirmed,
            Fee             = 300
        });
        await db.SaveChangesAsync();

        var svc    = new PaymentService(db);
        var model  = new PaymentRecordViewModel { AppointmentId = 3, Amount = 300, PaymentMethod = PaymentMethod.CreditCard };
        var result = await svc.RecordPaymentAsync(model, "secretary-id");

        Assert.Null(result);
    }

    [Fact]
    public async Task RecordPayment_AppointmentNotFound_ReturnsNull()
    {
        using var db = CreateDb();
        var svc    = new PaymentService(db);
        var model  = new PaymentRecordViewModel { AppointmentId = 999, Amount = 100, PaymentMethod = PaymentMethod.Cash };
        var result = await svc.RecordPaymentAsync(model, "secretary-id");

        Assert.Null(result);
    }

    [Fact]
    public async Task RecordPayment_InvoiceNumberFormat_IsCorrect()
    {
        using var db = CreateDb();
        SeedCompletedAppointment(db, appointmentId: 10, alreadyPaid: false);

        var svc    = new PaymentService(db);
        var model  = new PaymentRecordViewModel { AppointmentId = 10, Amount = 750, PaymentMethod = PaymentMethod.BankTransfer };
        var invoice = await svc.RecordPaymentAsync(model, "secretary-id");

        // Format: INV-YYYYMMDD-00010
        Assert.Matches(@"^INV-\d{8}-\d{5}$", invoice!);
    }

    // ─── Seed helpers ─────────────────────────────────────────────────────────

    private static void SeedCompletedAppointment(AppDbContext db, int appointmentId, bool alreadyPaid)
    {
        db.Appointments.Add(new Appointment
        {
            AppointmentId   = appointmentId,
            CustomerId      = 1,
            DoctorId        = 1,
            ServiceId       = 1,
            AppointmentDate = DateTime.UtcNow.AddDays(-1),
            Status          = AppointmentStatus.Completed,
            Fee             = 500
        });
        db.SaveChanges();

        if (alreadyPaid)
        {
            db.Payments.Add(new Payment
            {
                AppointmentId    = appointmentId,
                Amount           = 500,
                PaymentMethod    = PaymentMethod.Cash,
                PaidAt           = DateTime.UtcNow,
                RecordedByUserId = "secretary-id",
                InvoiceNumber    = $"INV-existing-{appointmentId}"
            });
            db.SaveChanges();
        }
    }
}
