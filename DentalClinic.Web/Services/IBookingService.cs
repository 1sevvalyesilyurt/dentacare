using DentalClinic.Web.Models;
using DentalClinic.Web.ViewModels;

namespace DentalClinic.Web.Services
{
    public interface IBookingService
    {
        /// <summary>
        /// Returns available (unbooked) time slots for a given doctor on a given date.
        /// Excludes slots that fall during doctor leave periods or have already passed today.
        /// </summary>
        Task<List<DateTime>> GetAvailableSlotsAsync(int doctorId, DateTime date, CancellationToken ct = default);

        /// <summary>
        /// Creates a new appointment after validating slot availability (BR-01) and leave (BR-05).
        /// Returns the created Appointment, or null if the slot is taken.
        /// </summary>
        Task<Appointment?> CreateAppointmentAsync(BookingViewModel model, string customerId, CancellationToken ct = default);

        /// <summary>
        /// Cancels an appointment. Only Pending/Confirmed appointments can be cancelled (BR-02).
        /// Pass isSecretary=true only when called from a Secretary-authorized controller action.
        /// </summary>
        Task<bool> CancelAppointmentAsync(int appointmentId, string requestingUserId, bool isSecretary = false, CancellationToken ct = default);

        /// <summary>
        /// Marks an appointment as Completed. Used by the Doctor.
        /// </summary>
        Task<bool> CompleteAppointmentAsync(int appointmentId, int doctorId, CancellationToken ct = default);
    }
}
