using DentalClinic.Web.Models;
using DentalClinic.Web.ViewModels;

namespace DentalClinic.Web.Services
{
    public interface IBookingService
    {
        /// <summary>
        /// Returns available (unbooked) time slots for a given doctor on a given date.
        /// Excludes slots that fall during doctor leave periods.
        /// </summary>
        Task<List<DateTime>> GetAvailableSlotsAsync(int doctorId, DateTime date);

        /// <summary>
        /// Creates a new appointment after validating slot availability (BR-01) and leave (BR-05).
        /// Returns the created Appointment, or null if the slot is taken.
        /// </summary>
        Task<Appointment?> CreateAppointmentAsync(BookingViewModel model, string customerId);

        /// <summary>
        /// Cancels an appointment. Only Pending/Confirmed appointments can be cancelled (BR-02).
        /// </summary>
        Task<bool> CancelAppointmentAsync(int appointmentId, string requestingUserId);

        /// <summary>
        /// Marks an appointment as Completed. Used by the Doctor.
        /// </summary>
        Task<bool> CompleteAppointmentAsync(int appointmentId, int doctorId);
    }
}
