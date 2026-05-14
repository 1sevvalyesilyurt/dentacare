using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using DentalClinic.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Web.Services
{
    public class BookingService : IBookingService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<BookingService> _logger;

        public BookingService(AppDbContext db, ILogger<BookingService> logger)
        {
            _db     = db;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<List<DateTime>> GetAvailableSlotsAsync(int doctorId, DateTime date, CancellationToken ct = default)
        {
            var doctor = await _db.Doctors.FindAsync(new object[] { doctorId }, ct);
            if (doctor == null) return new List<DateTime>();

            // Guard: misconfigured doctor record would cause infinite loop
            if (doctor.SlotDurationMinutes <= 0) return new List<DateTime>();

            // Generate all possible slots for the given day
            var slots   = new List<DateTime>();
            var current = date.Date + doctor.WorkingHoursStart;
            var endTime = date.Date + doctor.WorkingHoursEnd;

            while (current < endTime)
            {
                slots.Add(current);
                current = current.AddMinutes(doctor.SlotDurationMinutes);
            }

            // Remove already-booked slots (BR-01)
            var bookedTimes = await _db.Appointments
                .Where(a => a.DoctorId == doctorId
                         && a.AppointmentDate.Date == date.Date
                         && a.Status != AppointmentStatus.Cancelled)
                .Select(a => a.AppointmentDate)
                .ToListAsync(ct);

            // Remove slots during doctor leave (BR-05)
            var isOnLeave = await _db.DoctorLeaves
                .AnyAsync(l => l.DoctorId == doctorId
                            && l.StartDate.Date <= date.Date
                            && l.EndDate.Date >= date.Date, ct);

            if (isOnLeave) return new List<DateTime>();

            // For today, exclude slots that have already passed
            var now = DateTime.UtcNow;
            return slots
                .Where(s => !bookedTimes.Contains(s))
                .Where(s => s > now)
                .ToList();
        }

        /// <inheritdoc/>
        public async Task<Appointment?> CreateAppointmentAsync(BookingViewModel model, string userId, CancellationToken ct = default)
        {
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.UserId == userId, ct);
            if (customer == null) return null;

            var service = await _db.Services.FindAsync(new object[] { model.ServiceId }, ct);
            if (service == null) return null;

            // Server-side slot validation: the requested time must be a real slot
            var validSlots = await GetAvailableSlotsAsync(model.DoctorId, model.AppointmentDate.Date, ct);
            if (!validSlots.Contains(model.AppointmentDate)) return null;

            // BR-01 pre-check (optimistic; the unique index is the final guard)
            bool slotTaken = await _db.Appointments.AnyAsync(a =>
                a.DoctorId == model.DoctorId
                && a.AppointmentDate == model.AppointmentDate
                && a.Status != AppointmentStatus.Cancelled, ct);

            if (slotTaken) return null;

            var appointment = new Appointment
            {
                CustomerId      = customer.CustomerId,
                DoctorId        = model.DoctorId,
                ServiceId       = model.ServiceId,
                AppointmentDate = model.AppointmentDate,
                Status          = AppointmentStatus.Confirmed,
                PatientNote     = model.PatientNote,
                Fee             = service.BaseFee,
                CreatedAt       = DateTime.UtcNow
            };

            _db.Appointments.Add(appointment);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Unique constraint violation: another request won the race for this slot
                return null;
            }

            _db.Notifications.Add(new Notification
            {
                CustomerId    = customer.CustomerId,
                AppointmentId = appointment.AppointmentId,
                Message       = $"Your appointment is confirmed for {appointment.AppointmentDate:dddd, dd MMM yyyy} at {appointment.AppointmentDate:HH:mm}.",
                CreatedAt     = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "AUDIT Appointment created. AppointmentId={AppointmentId} CustomerId={CustomerId} DoctorId={DoctorId} Date={Date}",
                appointment.AppointmentId, customer.CustomerId, model.DoctorId, appointment.AppointmentDate);

            return appointment;
        }

        /// <inheritdoc/>
        public async Task<bool> CancelAppointmentAsync(int appointmentId, string userId, bool isSecretary = false, CancellationToken ct = default)
        {
            var appointment = await _db.Appointments
                .Include(a => a.Customer)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

            if (appointment == null) return false;

            // BR-02: Only Pending or Confirmed can be cancelled
            if (appointment.Status != AppointmentStatus.Pending &&
                appointment.Status != AppointmentStatus.Confirmed)
                return false;

            // Customers can only cancel their own appointment; secretaries can cancel any
            bool isOwner = appointment.Customer?.UserId == userId;
            if (!isOwner && !isSecretary) return false;

            appointment.Status = AppointmentStatus.Cancelled;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "AUDIT Appointment cancelled. AppointmentId={AppointmentId} CancelledBy={UserId} IsSecretary={IsSecretary}",
                appointmentId, userId, isSecretary);

            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> CompleteAppointmentAsync(int appointmentId, int doctorId, CancellationToken ct = default)
        {
            var appointment = await _db.Appointments
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId && a.DoctorId == doctorId, ct);

            if (appointment == null || appointment.Status != AppointmentStatus.Confirmed)
                return false;

            appointment.Status = AppointmentStatus.Completed;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "AUDIT Appointment completed. AppointmentId={AppointmentId} DoctorId={DoctorId}",
                appointmentId, doctorId);

            return true;
        }
    }
}
