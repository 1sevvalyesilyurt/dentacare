using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using DentalClinic.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Web.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(AppDbContext db, ILogger<PaymentService> logger)
        {
            _db     = db;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<string?> RecordPaymentAsync(PaymentRecordViewModel model, string secretaryUserId, CancellationToken ct = default)
        {
            var appointment = await _db.Appointments
                .Include(a => a.Payment)
                .FirstOrDefaultAsync(a => a.AppointmentId == model.AppointmentId, ct);

            if (appointment == null) return null;

            // BR-03: Must be Completed
            if (appointment.Status != AppointmentStatus.Completed) return null;

            // BR-03: Prevent double payment
            if (appointment.Payment != null) return null;

            var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{appointment.AppointmentId:D5}";

            var payment = new Payment
            {
                AppointmentId    = model.AppointmentId,
                Amount           = model.Amount,
                PaymentMethod    = model.PaymentMethod,
                PaidAt           = DateTime.UtcNow,
                RecordedByUserId = secretaryUserId,
                InvoiceNumber    = invoiceNumber
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "AUDIT Payment recorded. Invoice={Invoice} AppointmentId={AppointmentId} Amount={Amount} Method={Method} RecordedBy={SecretaryId}",
                invoiceNumber, model.AppointmentId, model.Amount, model.PaymentMethod, secretaryUserId);

            return invoiceNumber;
        }

        /// <inheritdoc/>
        public async Task<List<Appointment>> GetUnpaidCompletedAppointmentsAsync(CancellationToken ct = default)
        {
            return await _db.Appointments
                .Include(a => a.Customer!.User)
                .Include(a => a.Doctor!.User)
                .Include(a => a.Service)
                .Where(a => a.Status == AppointmentStatus.Completed && a.Payment == null)
                .OrderBy(a => a.AppointmentDate)
                .ToListAsync(ct);
        }
    }
}
