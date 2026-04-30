using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using DentalClinic.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Web.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _db;

        public PaymentService(AppDbContext db)
        {
            _db = db;
        }

        /// <inheritdoc/>
        public async Task<string?> RecordPaymentAsync(PaymentRecordViewModel model, string secretaryUserId)
        {
            var appointment = await _db.Appointments
                .Include(a => a.Payment)
                .FirstOrDefaultAsync(a => a.AppointmentId == model.AppointmentId);

            if (appointment == null) return null;

            // BR-03: Must be Completed
            if (appointment.Status != AppointmentStatus.Completed) return null;

            // BR-03: Prevent double payment
            if (appointment.Payment != null) return null;

            var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{appointment.AppointmentId:D5}";

            var payment = new Payment
            {
                AppointmentId = model.AppointmentId,
                Amount = model.Amount,
                PaymentMethod = model.PaymentMethod,
                PaidAt = DateTime.UtcNow,
                RecordedByUserId = secretaryUserId,
                InvoiceNumber = invoiceNumber
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();
            return invoiceNumber;
        }

        /// <inheritdoc/>
        public async Task<List<Appointment>> GetUnpaidCompletedAppointmentsAsync()
        {
            return await _db.Appointments
                .Include(a => a.Customer!.User)
                .Include(a => a.Doctor!.User)
                .Include(a => a.Service)
                .Where(a => a.Status == AppointmentStatus.Completed && a.Payment == null)
                .OrderBy(a => a.AppointmentDate)
                .ToListAsync();
        }
    }
}
