using DentalClinic.Web.Models;
using DentalClinic.Web.ViewModels;

namespace DentalClinic.Web.Services
{
    public interface IPaymentService
    {
        /// <summary>
        /// Records a payment for a completed appointment.
        /// Business Rule BR-03: Appointment must be Completed; no duplicate payments allowed.
        /// Returns the generated InvoiceNumber, or null if validation fails.
        /// </summary>
        Task<string?> RecordPaymentAsync(PaymentRecordViewModel model, string secretaryUserId);

        /// <summary>
        /// Retrieves all completed appointments that do not yet have a Payment record.
        /// </summary>
        Task<List<Appointment>> GetUnpaidCompletedAppointmentsAsync();
    }
}
