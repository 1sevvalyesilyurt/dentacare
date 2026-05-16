using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Web.Controllers
{
    /// <summary>
    /// Handles appointment booking and management for the Customer role.
    /// UC-C03, UC-C04, UC-C05, UC-C06, UC-C07
    /// </summary>
    [Authorize(Roles = "Customer")]
    public class AppointmentController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IBookingService _bookingService;
        private readonly UserManager<ApplicationUser> _userManager;

        public AppointmentController(
            AppDbContext db,
            IBookingService bookingService,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _bookingService = bookingService;
            _userManager = userManager;
        }

        // GET /Appointment — Upcoming appointments (UC-C06)
        public async Task<IActionResult> Index()
        {
            var ct   = HttpContext.RequestAborted;
            var user = await _userManager.GetUserAsync(User);
            var customer = await _db.Customers
                .FirstOrDefaultAsync(c => c.UserId == user!.Id, ct);

            if (customer == null) return RedirectToAction("Register", "Account");

            var appointments = await _db.Appointments
                .Include(a => a.Doctor!.User)
                .Include(a => a.Service)
                .Where(a => a.CustomerId == customer.CustomerId
                         && a.AppointmentDate >= DateTime.UtcNow
                         && a.Status != AppointmentStatus.Cancelled)
                .OrderBy(a => a.AppointmentDate)
                .ToListAsync(ct);

            // Notifications are marked as read only in NotificationController.All()
            // so the navbar badge reflects the true unread count until explicitly visited.

            return View(appointments);
        }

        // GET /Appointment/Book — Booking form (UC-C03, UC-C04)
        [HttpGet]
        public async Task<IActionResult> Book()
        {
            var ct    = HttpContext.RequestAborted;
            var model = new BookingViewModel
            {
                AvailableDoctors = await _db.Doctors
                    .Include(d => d.User)
                    .Where(d => d.IsActive)
                    .Select(d => new DoctorSelectItem
                    {
                        DoctorId    = d.DoctorId,
                        DisplayName = $"Dr. {d.User!.FullName} — {d.Specialty}"
                    }).ToListAsync(ct),

                AvailableServices = await _db.Services
                    .Where(s => s.IsActive)
                    .Select(s => new ServiceSelectItem
                    {
                        ServiceId   = s.ServiceId,
                        DisplayName = $"{s.Name} — ₺{s.BaseFee:N0} ({s.DurationMinutes} min)"
                    }).ToListAsync(ct)
            };
            return View(model);
        }

        // POST /Appointment/Book (UC-C04)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Book(BookingViewModel model)
        {
            var ct = HttpContext.RequestAborted;

            if (!ModelState.IsValid)
            {
                model.AvailableDoctors = await _db.Doctors.Include(d => d.User)
                    .Where(d => d.IsActive)
                    .Select(d => new DoctorSelectItem
                    {
                        DoctorId    = d.DoctorId,
                        DisplayName = $"Dr. {d.User!.FullName} — {d.Specialty}"
                    }).ToListAsync(ct);
                model.AvailableServices = await _db.Services.Where(s => s.IsActive)
                    .Select(s => new ServiceSelectItem
                    {
                        ServiceId   = s.ServiceId,
                        DisplayName = $"{s.Name} — ₺{s.BaseFee:N0} ({s.DurationMinutes} min)"
                    }).ToListAsync(ct);
                return View(model);
            }

            var user        = await _userManager.GetUserAsync(User);
            var appointment = await _bookingService.CreateAppointmentAsync(model, user!.Id, ct);

            if (appointment == null)
            {
                ModelState.AddModelError(string.Empty,
                    "This time slot is no longer available. Please select another slot.");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Your appointment has been confirmed for {appointment.AppointmentDate:dddd, dd MMM yyyy 'at' HH:mm}.";
            return RedirectToAction(nameof(Confirmed), new { id = appointment.AppointmentId });
        }

        // GET /Appointment/Slots?doctorId=1&date=2026-05-10 — AJAX endpoint
        [HttpGet]
        public async Task<IActionResult> Slots(int doctorId, DateTime date)
        {
            try
            {
                var slots = await _bookingService.GetAvailableSlotsAsync(
                    doctorId, date, HttpContext.RequestAborted);
                return Json(slots.Select(s => s.ToString("HH:mm")));
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "Unable to load available slots. Please try again." });
            }
        }

        // GET /Appointment/Confirmed/{id}
        public async Task<IActionResult> Confirmed(int id)
        {
            var ct   = HttpContext.RequestAborted;
            var user = await _userManager.GetUserAsync(User);
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.UserId == user!.Id, ct);
            if (customer == null) return NotFound();

            var appointment = await _db.Appointments
                .Include(a => a.Doctor!.User)
                .Include(a => a.Service)
                .FirstOrDefaultAsync(a => a.AppointmentId == id
                                       && a.CustomerId == customer.CustomerId, ct);

            if (appointment == null) return NotFound();
            return View(appointment);
        }

        // GET /Appointment/History — Past appointments (UC-C07)
        public async Task<IActionResult> History()
        {
            var ct   = HttpContext.RequestAborted;
            var user = await _userManager.GetUserAsync(User);
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.UserId == user!.Id, ct);
            if (customer == null) return NotFound();

            var appointments = await _db.Appointments
                .Include(a => a.Doctor!.User)
                .Include(a => a.Service)
                .Include(a => a.Payment)
                .Where(a => a.CustomerId == customer.CustomerId
                         && (a.Status == AppointmentStatus.Completed
                          || a.Status == AppointmentStatus.Cancelled
                          || a.AppointmentDate < DateTime.UtcNow))
                .OrderByDescending(a => a.AppointmentDate)
                .ToListAsync(ct);

            return View(appointments);
        }

        // POST /Appointment/Cancel/{id} (UC-C05)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var user    = await _userManager.GetUserAsync(User);
            var success = await _bookingService.CancelAppointmentAsync(
                id, user!.Id, isSecretary: false, HttpContext.RequestAborted);

            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                success ? "Appointment cancelled successfully." : "This appointment cannot be cancelled.";

            return RedirectToAction(nameof(Index));
        }
    }
}
