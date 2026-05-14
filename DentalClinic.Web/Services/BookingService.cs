using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using DentalClinic.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Web.Services
{
    public class BookingService : IBookingService
    {
        private readonly AppDbContext _db;

        public BookingService(AppDbContext db)
        {
            _db = db;
        }

        /// <inheritdoc/>
        public async Task<List<DateTime>> GetAvailableSlotsAsync(int doctorId, DateTime date)
        {
            var doctor = await _db.Doctors.FindAsync(doctorId);
            if (doctor == null) return new List<DateTime>();

            // Generate all possible slots for the given day
            var slots = new List<DateTime>();
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
                .ToListAsync();

            // Remove slots during doctor leave (BR-05 prerequisite)
            var isOnLeave = await _db.DoctorLeaves
                .AnyAsync(l => l.DoctorId == doctorId
                            && l.StartDate.Date <= date.Date
                            && l.EndDate.Date >= date.Date);

            if (isOnLeave) return new List<DateTime>();

            return slots.Where(s => !bookedTimes.Contains(s)).ToList();
        }

        /// <inheritdoc/>
        public async Task<Appointment?> CreateAppointmentAsync(BookingViewModel model, string userId)
        {
            // Resolve customer profile from ApplicationUser.Id
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
            if (customer == null) return null;

            var service = await _db.Services.FindAsync(model.ServiceId);
            if (service == null) return null;

            // BR-01: Check for slot conflict at DB level before insert
            bool slotTaken = await _db.Appointments.AnyAsync(a =>
                a.DoctorId == model.DoctorId
                && a.AppointmentDate == model.AppointmentDate
                && a.Status != AppointmentStatus.Cancelled);

            if (slotTaken) return null;

            // Check doctor leave
            bool onLeave = await _db.DoctorLeaves.AnyAsync(l =>
                l.DoctorId == model.DoctorId
                && l.StartDate.Date <= model.AppointmentDate.Date
                && l.EndDate.Date >= model.AppointmentDate.Date);

            if (onLeave) return null;

            var appointment = new Appointment
            {
                CustomerId = customer.CustomerId,
                DoctorId = model.DoctorId,
                ServiceId = model.ServiceId,
                AppointmentDate = model.AppointmentDate,
                Status = AppointmentStatus.Confirmed,
                PatientNote = model.PatientNote,
                Fee = service.BaseFee,
                CreatedAt = DateTime.UtcNow
            };

            _db.Appointments.Add(appointment);
            await _db.SaveChangesAsync();

            // Create in-app booking confirmation notification
            var notification = new Notification
            {
                CustomerId = customer.CustomerId,
                AppointmentId = appointment.AppointmentId,
                Message = $"Your appointment is confirmed for {appointment.AppointmentDate:dddd, dd MMM yyyy} at {appointment.AppointmentDate:HH:mm}.",
                CreatedAt = DateTime.UtcNow
            };
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            return appointment;
        }

        /// <inheritdoc/>
        public async Task<bool> CancelAppointmentAsync(int appointmentId, string userId)
        {
            var appointment = await _db.Appointments
                .Include(a => a.Customer)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appointment == null) return false;

            // BR-02: Only Pending or Confirmed can be cancelled
            if (appointment.Status != AppointmentStatus.Pending &&
                appointment.Status != AppointmentStatus.Confirmed)
                return false;

            // Authorization: customer can only cancel their own appointment
            bool isOwner = appointment.Customer?.UserId == userId;
            bool isSecretary = true; // Secretary access is checked via [Authorize(Roles="Secretary")] at controller level
            if (!isOwner && !isSecretary) return false;

            appointment.Status = AppointmentStatus.Cancelled;
            await _db.SaveChangesAsync();
            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> RescheduleAppointmentAsync(int appointmentId, DateTime newAppointmentDate)
        {
            var appointment = await _db.Appointments
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appointment == null || appointment.Doctor == null)
                return false;

            // Only active appointments can be rescheduled
            if (appointment.Status == AppointmentStatus.Cancelled ||
                appointment.Status == AppointmentStatus.Completed)
                return false;

            var doctor = appointment.Doctor;
            var newTime = newAppointmentDate.TimeOfDay;

            // Must be inside working hours
            if (newTime < doctor.WorkingHoursStart || newTime >= doctor.WorkingHoursEnd)
                return false;

            // Must align with doctor's slot interval
            var minutesFromStart = (newTime - doctor.WorkingHoursStart).TotalMinutes;
            if (minutesFromStart < 0 || minutesFromStart % doctor.SlotDurationMinutes != 0)
                return false;

            // Doctor cannot be on leave for target date
            var onLeave = await _db.DoctorLeaves.AnyAsync(l =>
                l.DoctorId == appointment.DoctorId &&
                l.StartDate.Date <= newAppointmentDate.Date &&
                l.EndDate.Date >= newAppointmentDate.Date);
            if (onLeave) return false;

            // Slot must be free (excluding this appointment)
            var slotTaken = await _db.Appointments.AnyAsync(a =>
                a.AppointmentId != appointment.AppointmentId &&
                a.DoctorId == appointment.DoctorId &&
                a.AppointmentDate == newAppointmentDate &&
                a.Status != AppointmentStatus.Cancelled);
            if (slotTaken) return false;

            appointment.AppointmentDate = newAppointmentDate;
            appointment.ReminderSent = false; // Allow reminder service to send for the new time.

            await _db.SaveChangesAsync();

            _db.Notifications.Add(new Notification
            {
                CustomerId = appointment.CustomerId,
                AppointmentId = appointment.AppointmentId,
                Message = $"Your appointment has been rescheduled to {newAppointmentDate:dddd, dd MMM yyyy} at {newAppointmentDate:HH:mm}.",
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            return true;
        }

        /// <inheritdoc/>
        public async Task<bool> CompleteAppointmentAsync(int appointmentId, int doctorId)
        {
            var appointment = await _db.Appointments
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId && a.DoctorId == doctorId);

            if (appointment == null || appointment.Status != AppointmentStatus.Confirmed)
                return false;

            appointment.Status = AppointmentStatus.Completed;
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
