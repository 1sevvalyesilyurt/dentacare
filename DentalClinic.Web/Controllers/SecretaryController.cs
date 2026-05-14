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
    /// Secretary (Admin) panel controller.
    /// UC-S02, UC-S03, UC-S04, UC-S05, UC-S06, UC-S07, UC-S08, UC-S09, UC-S10
    /// </summary>
    [Authorize(Roles = "Secretary")]
    public class SecretaryController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IBookingService _bookingService;
        private readonly IPaymentService _paymentService;
        private readonly UserManager<ApplicationUser> _userManager;

        public SecretaryController(
            AppDbContext db,
            IBookingService bookingService,
            IPaymentService paymentService,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _bookingService = bookingService;
            _paymentService = paymentService;
            _userManager = userManager;
        }

        // GET /Secretary/Dashboard — Overview stats
        public async Task<IActionResult> Dashboard()
        {
            var today = DateTime.UtcNow.Date;

            ViewBag.TodayCount = await _db.Appointments
                .CountAsync(a => a.AppointmentDate.Date == today
                             && a.Status != AppointmentStatus.Cancelled);
            ViewBag.PendingPayments = await _db.Appointments
                .CountAsync(a => a.Status == AppointmentStatus.Completed && a.Payment == null);
            ViewBag.TotalDoctors = await _db.Doctors.CountAsync(d => d.IsActive);
            ViewBag.TotalCustomers = await _db.Customers.CountAsync();

            var recentAppointments = await _db.Appointments
                .Include(a => a.Customer!.User)
                .Include(a => a.Doctor!.User)
                .Include(a => a.Service)
                .OrderByDescending(a => a.CreatedAt)
                .Take(10)
                .ToListAsync();

            return View(recentAppointments);
        }

        // GET /Secretary/Calendar — All-doctors calendar (UC-S03)
        public async Task<IActionResult> Calendar(DateTime? date)
        {
            var target = date ?? DateTime.Today;
            var appointments = await _db.Appointments
                .Include(a => a.Customer!.User)
                .Include(a => a.Doctor!.User)
                .Include(a => a.Service)
                .Where(a => a.AppointmentDate.Date == target.Date
                         && a.Status != AppointmentStatus.Cancelled)
                .OrderBy(a => a.DoctorId)
                .ThenBy(a => a.AppointmentDate)
                .ToListAsync();

            ViewBag.SelectedDate = target;
            return View(appointments);
        }

        // GET /Secretary/CreateAppointment — Manual booking form (UC-S04)
        [HttpGet]
        public async Task<IActionResult> CreateAppointment()
        {
            await PopulateBookingDropdowns();
            return View(new BookingViewModel());
        }

        // POST /Secretary/CreateAppointment (UC-S04)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAppointment(BookingViewModel model, string customerUserId)
        {
            if (!ModelState.IsValid)
            {
                await PopulateBookingDropdowns();
                return View(model);
            }

            var appointment = await _bookingService.CreateAppointmentAsync(model, customerUserId);
            if (appointment == null)
            {
                ModelState.AddModelError(string.Empty, "Slot is unavailable.");
                await PopulateBookingDropdowns();
                return View(model);
            }

            // Tag as manually created by the secretary
            var secretary = await _userManager.GetUserAsync(User);
            appointment.CreatedByUserId = secretary!.Id;
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Appointment created successfully.";
            return RedirectToAction(nameof(Calendar));
        }

        // POST /Secretary/CancelAppointment/{id} (UC-S05)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelAppointment(int id)
        {
            var secretary = await _userManager.GetUserAsync(User);
            await _bookingService.CancelAppointmentAsync(id, secretary!.Id, isSecretary: true);
            TempData["SuccessMessage"] = "Appointment cancelled.";
            return RedirectToAction(nameof(Calendar));
        }

        // GET /Secretary/Payments — Revenue dashboard (UC-S08)
        public async Task<IActionResult> Payments()
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            var vm = new PaymentDashboardViewModel
            {
                TotalRevenueToday = await _db.Payments
                    .Where(p => p.PaidAt.Date == now.Date)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0,

                TotalRevenueThisMonth = await _db.Payments
                    .Where(p => p.PaidAt >= monthStart)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0,

                PendingPaymentsCount = await _db.Appointments
                    .CountAsync(a => a.Status == AppointmentStatus.Completed && a.Payment == null),

                RecentPayments = await _db.Payments
                    .Include(p => p.Appointment!.Customer!.User)
                    .Include(p => p.Appointment!.Doctor!.User)
                    .OrderByDescending(p => p.PaidAt)
                    .Take(20)
                    .Select(p => new RecentPaymentRow
                    {
                        PaymentId = p.PaymentId,
                        InvoiceNumber = p.InvoiceNumber,
                        PatientName = p.Appointment!.Customer!.User!.FullName,
                        DoctorName = p.Appointment!.Doctor!.User!.FullName,
                        Amount = p.Amount,
                        Method = p.PaymentMethod,
                        PaidAt = p.PaidAt
                    }).ToListAsync(),

                DoctorEarnings = await _db.Doctors
                    .Include(d => d.User)
                    .Include(d => d.Appointments)
                        .ThenInclude(a => a.Payment)
                    .Where(d => d.IsActive)
                    .Select(d => new DoctorEarningsRow
                    {
                        DoctorName = d.User!.FullName,
                        Specialty = d.Specialty,
                        CompletedAppointments = d.Appointments.Count(a => a.Status == AppointmentStatus.Completed),
                        TotalRevenue = d.Appointments
                            .Where(a => a.Payment != null)
                            .Sum(a => a.Payment!.Amount),
                        DoctorShare = d.Appointments
                            .Where(a => a.Payment != null)
                            .Sum(a => a.Payment!.Amount) * d.CommissionRate
                    }).ToListAsync()
            };

            return View(vm);
        }

        // GET /Secretary/RecordPayment/{appointmentId} (UC-S06)
        [HttpGet]
        public async Task<IActionResult> RecordPayment(int appointmentId)
        {
            var appt = await _db.Appointments
                .Include(a => a.Customer!.User)
                .Include(a => a.Doctor!.User)
                .Include(a => a.Service)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appt == null) return NotFound();
            if (appt.Status != AppointmentStatus.Completed)
            {
                TempData["ErrorMessage"] = "Payment can only be recorded for Completed appointments.";
                return RedirectToAction(nameof(Payments));
            }

            var vm = new PaymentRecordViewModel
            {
                AppointmentId = appointmentId,
                PatientName = appt.Customer?.User?.FullName ?? "",
                DoctorName = appt.Doctor?.User?.FullName ?? "",
                ServiceName = appt.Service?.Name ?? "",
                AppointmentDate = appt.AppointmentDate,
                SuggestedFee = appt.Fee,
                Amount = appt.Fee
            };
            return View(vm);
        }

        // POST /Secretary/RecordPayment (UC-S06)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordPayment(PaymentRecordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var secretary = await _userManager.GetUserAsync(User);
            var invoiceNumber = await _paymentService.RecordPaymentAsync(model, secretary!.Id);

            if (invoiceNumber == null)
            {
                ModelState.AddModelError(string.Empty, "Payment could not be recorded. Appointment may already be paid.");
                return View(model);
            }

            TempData["SuccessMessage"] = $"Payment recorded. Invoice: {invoiceNumber}";
            return RedirectToAction(nameof(Payments));
        }

        // GET /Secretary/Customers — Customer list (UC-S09)
        public async Task<IActionResult> Customers()
        {
            var customers = await _db.Customers
                .Include(c => c.User)
                .Include(c => c.Appointments)
                .OrderBy(c => c.User!.FullName)
                .ToListAsync();
            return View(customers);
        }

        // GET /Secretary/Services — Service management (UC-S10)
        public async Task<IActionResult> Services()
        {
            var services = await _db.Services.OrderBy(s => s.Name).ToListAsync();
            return View(services);
        }

        // ─── Doctor Management (UC-S02) ────────────────────────────────────────────

        // GET /Secretary/Doctors — Doctor list
        public async Task<IActionResult> Doctors()
        {
            var doctors = await _db.Doctors
                .Include(d => d.User)
                .Include(d => d.Appointments)
                .OrderBy(d => d.User!.FullName)
                .ToListAsync();
            return View(doctors);
        }

        // GET /Secretary/CreateDoctor
        [HttpGet]
        public IActionResult CreateDoctor() => View(new CreateDoctorViewModel());

        // POST /Secretary/CreateDoctor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDoctor(CreateDoctorViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Check email is not already taken
            if (await _userManager.FindByEmailAsync(model.Email) != null)
            {
                ModelState.AddModelError("Email", "A user with this email already exists.");
                return View(model);
            }

            // 1. Create the ApplicationUser account
            var user = new ApplicationUser
            {
                UserName      = model.Email,
                Email         = model.Email,
                FullName      = model.FullName,
                PhoneNumber   = model.PhoneNumber,
                IsActive      = true,
                CreatedAt     = DateTime.UtcNow,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            // 2. Assign the Doctor role
            await _userManager.AddToRoleAsync(user, "Doctor");

            // 3. Create the Doctor profile record
            var doctor = new Doctor
            {
                UserId             = user.Id,
                Specialty          = model.Specialty,
                CommissionRate     = model.CommissionRate,
                WorkingHoursStart  = model.WorkingHoursStart,
                WorkingHoursEnd    = model.WorkingHoursEnd,
                SlotDurationMinutes = model.SlotDurationMinutes,
                IsActive           = true
            };

            _db.Doctors.Add(doctor);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Dr. {model.FullName} has been added successfully. Login: {model.Email}";
            return RedirectToAction(nameof(Doctors));
        }

        // POST /Secretary/ToggleDoctor/{id} — Activate / Deactivate
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleDoctor(int id)
        {
            var doctor = await _db.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.DoctorId == id);
            if (doctor == null) return NotFound();

            doctor.IsActive = !doctor.IsActive;
            if (doctor.User != null)
                doctor.User.IsActive = doctor.IsActive;

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Dr. {doctor.User?.FullName} is now {(doctor.IsActive ? "Active" : "Deactivated")}.";
            return RedirectToAction(nameof(Doctors));
        }

        // ─── Helpers ───────────────────────────────────────────────────────────────
        private async Task PopulateBookingDropdowns()
        {
            ViewBag.Doctors = await _db.Doctors.Include(d => d.User)
                .Where(d => d.IsActive).ToListAsync();
            ViewBag.Services = await _db.Services
                .Where(s => s.IsActive).ToListAsync();
            ViewBag.Customers = await _db.Customers.Include(c => c.User).ToListAsync();
        }
    }
}
