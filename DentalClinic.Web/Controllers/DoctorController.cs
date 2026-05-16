using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Web.Controllers
{
    /// <summary>
    /// Doctor dashboard controller.
    /// UC-D01, UC-D02, UC-D03, UC-D04, UC-D05, UC-D06
    /// Business Rule BR-04: Doctor can only see their own appointments.
    /// </summary>
    [Authorize(Roles = "Doctor")]
    public class DoctorController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IBookingService _bookingService;
        private readonly UserManager<ApplicationUser> _userManager;

        public DoctorController(
            AppDbContext db,
            IBookingService bookingService,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _bookingService = bookingService;
            _userManager = userManager;
        }

        private CancellationToken ct => HttpContext.RequestAborted;

        // GET /Doctor/Dashboard — Daily schedule + earnings (UC-D02, UC-D05)
        public async Task<IActionResult> Dashboard(DateTime? date)
        {
            var doctor = await GetCurrentDoctorAsync(ct);
            if (doctor == null) return Forbid();

            var target    = date?.Date ?? DateTime.UtcNow.Date;
            var weekStart = target.AddDays(-(int)target.DayOfWeek + 1);
            var weekEnd   = weekStart.AddDays(6);

            // BR-04: Only this doctor's appointments
            var todayAppts = await _db.Appointments
                .Include(a => a.Customer!.User)
                .Include(a => a.Service)
                .Where(a => a.DoctorId == doctor.DoctorId
                         && a.AppointmentDate.Date == target
                         && a.Status != AppointmentStatus.Cancelled)
                .OrderBy(a => a.AppointmentDate)
                .ToListAsync(ct);

            var weekAppts = await _db.Appointments
                .Include(a => a.Customer!.User)
                .Include(a => a.Service)
                .Where(a => a.DoctorId == doctor.DoctorId
                         && a.AppointmentDate.Date >= weekStart
                         && a.AppointmentDate.Date <= weekEnd
                         && a.Status != AppointmentStatus.Cancelled)
                .OrderBy(a => a.AppointmentDate)
                .ToListAsync(ct);

            // BR-06: Earnings = Fee × CommissionRate
            var vm = new DoctorDashboardViewModel
            {
                DoctorFullName = doctor.User?.FullName ?? "",
                Specialty      = doctor.Specialty,
                CommissionRate = doctor.CommissionRate,
                SelectedDate   = target,

                TodayAppointments = todayAppts.Select(a => new AppointmentSummary
                {
                    AppointmentId   = a.AppointmentId,
                    AppointmentDate = a.AppointmentDate,
                    PatientName     = a.Customer?.User?.FullName ?? "Unknown",
                    ServiceName     = a.Service?.Name ?? "",
                    PatientNote     = a.PatientNote,
                    Status          = a.Status,
                    Fee             = a.Fee,
                    DoctorEarning   = a.Fee * doctor.CommissionRate
                }).ToList(),

                WeekAppointments = weekAppts.Select(a => new AppointmentSummary
                {
                    AppointmentId   = a.AppointmentId,
                    AppointmentDate = a.AppointmentDate,
                    PatientName     = a.Customer?.User?.FullName ?? "Unknown",
                    ServiceName     = a.Service?.Name ?? "",
                    PatientNote     = a.PatientNote,
                    Status          = a.Status,
                    Fee             = a.Fee,
                    DoctorEarning   = a.Fee * doctor.CommissionRate
                }).ToList(),

                TodayEarnings = todayAppts
                    .Where(a => a.Status == AppointmentStatus.Completed)
                    .Sum(a => a.Fee * doctor.CommissionRate),

                WeekEarnings = weekAppts
                    .Where(a => a.Status == AppointmentStatus.Completed)
                    .Sum(a => a.Fee * doctor.CommissionRate)
            };

            return View(vm);
        }

        // POST /Doctor/CompleteAppointment/{id} (UC-D06)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteAppointment(int id)
        {
            var doctor = await GetCurrentDoctorAsync(ct);
            if (doctor == null) return Forbid();

            var success = await _bookingService.CompleteAppointmentAsync(id, doctor.DoctorId, ct);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                success ? "Appointment marked as completed." : "Could not complete this appointment.";

            return RedirectToAction(nameof(Dashboard));
        }

        // ─── Helper ──────────────────────────────────────────────────────────────
        private async Task<Doctor?> GetCurrentDoctorAsync(CancellationToken ct = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;
            return await _db.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == user.Id, ct);
        }
    }
}
