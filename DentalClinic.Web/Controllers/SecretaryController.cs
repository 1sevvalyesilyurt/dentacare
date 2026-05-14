using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

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
            var target = date?.Date ?? DateTime.Today;
            var appointments = await _db.Appointments
                .Include(a => a.Customer!.User)
                .Include(a => a.Doctor!.User)
                .Include(a => a.Service)
                .Include(a => a.Payment)
                .Where(a => a.AppointmentDate.Date == target
                         && a.Status != AppointmentStatus.Cancelled)
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.Doctor!.User!.FullName)
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
            await _bookingService.CancelAppointmentAsync(id, secretary!.Id);
            TempData["SuccessMessage"] = "Appointment cancelled.";
            return RedirectToAction(nameof(Calendar));
        }

        // GET /Secretary/RescheduleAppointment/{id} (UC-S05)
        [HttpGet]
        public async Task<IActionResult> RescheduleAppointment(int id)
        {
            var vm = new RescheduleAppointmentViewModel { AppointmentId = id };
            var loaded = await PopulateRescheduleContextAsync(vm);
            if (!loaded) return NotFound();
            return View(vm);
        }

        // POST /Secretary/RescheduleAppointment (UC-S05)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RescheduleAppointment(RescheduleAppointmentViewModel model)
        {
            var loaded = await PopulateRescheduleContextAsync(model);
            if (!loaded) return NotFound();

            if (!TimeSpan.TryParse(model.NewTime, out var slotTime))
                ModelState.AddModelError(nameof(model.NewTime), "Please select a valid time slot.");

            if (!ModelState.IsValid) return View(model);

            var newDateTime = model.NewDate.Date + slotTime;
            var success = await _bookingService.RescheduleAppointmentAsync(model.AppointmentId, newDateTime);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, "Reschedule failed. The slot may be unavailable or invalid.");
                await PopulateRescheduleContextAsync(model);
                return View(model);
            }

            TempData["SuccessMessage"] = $"Appointment moved to {newDateTime:dddd, dd MMM yyyy HH:mm}.";
            return RedirectToAction(nameof(Dashboard));
        }

        // GET /Secretary/AvailableSlots?doctorId=1&date=2026-05-10&appointmentId=12
        [HttpGet]
        public async Task<IActionResult> AvailableSlots(int doctorId, DateTime date, int? appointmentId = null)
        {
            var slots = await _bookingService.GetAvailableSlotsAsync(doctorId, date);
            var slotTimes = slots.Select(s => s.ToString("HH:mm")).ToList();

            // Keep the current appointment time selectable when rescheduling the same record.
            if (appointmentId.HasValue)
            {
                var appt = await _db.Appointments.FirstOrDefaultAsync(a => a.AppointmentId == appointmentId.Value);
                if (appt != null && appt.DoctorId == doctorId && appt.AppointmentDate.Date == date.Date)
                {
                    var current = appt.AppointmentDate.ToString("HH:mm");
                    if (!slotTimes.Contains(current))
                        slotTimes.Add(current);
                }
            }

            return Json(slotTimes.OrderBy(t => t));
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

                PendingPayments = (await _paymentService.GetUnpaidCompletedAppointmentsAsync())
                    .Select(a => new PendingPaymentRow
                    {
                        AppointmentId = a.AppointmentId,
                        PatientName = a.Customer!.User!.FullName,
                        DoctorName = a.Doctor!.User!.FullName,
                        ServiceName = a.Service!.Name,
                        AppointmentDate = a.AppointmentDate,
                        Fee = a.Fee
                    }).ToList(),

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

        // GET /Secretary/PrintInvoice/{paymentId} (UC-S07)
        [HttpGet]
        public async Task<IActionResult> PrintInvoice(int paymentId)
        {
            var payment = await _db.Payments
                .Include(p => p.Appointment!.Customer!.User)
                .Include(p => p.Appointment!.Doctor!.User)
                .Include(p => p.Appointment!.Service)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);

            if (payment == null || payment.Appointment == null)
                return NotFound();

            var invoiceNumber = string.IsNullOrWhiteSpace(payment.InvoiceNumber)
                ? $"INV-{DateTime.UtcNow:yyyyMMdd}-{payment.PaymentId:D5}"
                : payment.InvoiceNumber;

            QuestPDF.Settings.License = LicenseType.Community;

            var pdfBytes = Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(32);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("DentaCare Clinic").Bold().FontSize(20);
                        col.Item().Text($"Invoice: {invoiceNumber}").SemiBold();
                        col.Item().Text($"Issue Date: {payment.PaidAt:dd MMM yyyy HH:mm}");
                    });

                    page.Content().PaddingVertical(16).Column(col =>
                    {
                        col.Spacing(8);

                        col.Item().Text($"Patient: {payment.Appointment.Customer?.User?.FullName ?? "-"}");
                        col.Item().Text($"Doctor: Dr. {payment.Appointment.Doctor?.User?.FullName ?? "-"}");
                        col.Item().Text($"Service: {payment.Appointment.Service?.Name ?? "-"}");
                        col.Item().Text($"Appointment Date: {payment.Appointment.AppointmentDate:dd MMM yyyy HH:mm}");

                        col.Item().PaddingTop(14).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Payment Method").SemiBold();
                            row.ConstantItem(160).AlignRight().Text(payment.PaymentMethod.ToString());
                        });

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Amount Paid").SemiBold();
                            row.ConstantItem(160).AlignRight().Text($"₺{payment.Amount:N2}").SemiBold();
                        });
                    });

                    page.Footer().AlignCenter().Text("Generated by DentaCare").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            }).GeneratePdf();

            return File(pdfBytes, "application/pdf", $"{invoiceNumber}.pdf");
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

        // POST /Secretary/CreateService (UC-S10)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateService([Bind("Name,Description,BaseFee,DurationMinutes")] Service model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Invalid service data.";
                return RedirectToAction(nameof(Services));
            }

            var exists = await _db.Services.AnyAsync(s => s.Name.ToLower() == model.Name.ToLower());
            if (exists)
            {
                TempData["ErrorMessage"] = "A service with the same name already exists.";
                return RedirectToAction(nameof(Services));
            }

            _db.Services.Add(new Service
            {
                Name = model.Name.Trim(),
                Description = model.Description?.Trim(),
                BaseFee = model.BaseFee,
                DurationMinutes = model.DurationMinutes,
                IsActive = true
            });

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Service added successfully.";
            return RedirectToAction(nameof(Services));
        }

        // POST /Secretary/EditService/{id} (UC-S10)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditService(int id, [Bind("Name,Description,BaseFee,DurationMinutes")] Service model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Invalid service update.";
                return RedirectToAction(nameof(Services));
            }

            var service = await _db.Services.FirstOrDefaultAsync(s => s.ServiceId == id);
            if (service == null) return NotFound();

            var duplicateName = await _db.Services
                .AnyAsync(s => s.ServiceId != id && s.Name.ToLower() == model.Name.ToLower());
            if (duplicateName)
            {
                TempData["ErrorMessage"] = "Another service with this name already exists.";
                return RedirectToAction(nameof(Services));
            }

            service.Name = model.Name.Trim();
            service.Description = model.Description?.Trim();
            service.BaseFee = model.BaseFee;
            service.DurationMinutes = model.DurationMinutes;

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Service updated successfully.";
            return RedirectToAction(nameof(Services));
        }

        // POST /Secretary/ToggleService/{id} (UC-S10)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleService(int id)
        {
            var service = await _db.Services.FirstOrDefaultAsync(s => s.ServiceId == id);
            if (service == null) return NotFound();

            service.IsActive = !service.IsActive;
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Service '{service.Name}' is now {(service.IsActive ? "Active" : "Inactive")}.";
            return RedirectToAction(nameof(Services));
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

        // ─── Doctor Leave Management (UC-S02 extension) ──────────────────────────

        // GET /Secretary/DoctorLeaves — List all leave records
        [HttpGet]
        public async Task<IActionResult> DoctorLeaves()
        {
            var leaves = await _db.DoctorLeaves
                .Include(l => l.Doctor!)
                    .ThenInclude(d => d.User)
                .OrderByDescending(l => l.StartDate)
                .ToListAsync();

            return View(leaves);
        }

        // GET /Secretary/AddLeave/{doctorId} — Create leave form
        [HttpGet]
        public async Task<IActionResult> AddLeave(int doctorId)
        {
            var doctor = await _db.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.DoctorId == doctorId);

            if (doctor == null)
            {
                TempData["ErrorMessage"] = "Doctor not found.";
                return RedirectToAction(nameof(Doctors));
            }

            ViewBag.Doctor = doctor;
            return View(new DoctorLeave
            {
                DoctorId = doctorId,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today
            });
        }

        // POST /Secretary/AddLeave — Save leave
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddLeave(DoctorLeave model)
        {
            var doctor = await _db.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.DoctorId == model.DoctorId);

            if (doctor == null)
            {
                TempData["ErrorMessage"] = "Doctor not found.";
                return RedirectToAction(nameof(Doctors));
            }

            if (model.StartDate.Date > model.EndDate.Date)
                ModelState.AddModelError(string.Empty, "Start date cannot be later than end date.");

            if (!ModelState.IsValid)
            {
                ViewBag.Doctor = doctor;
                return View(model);
            }

            _db.DoctorLeaves.Add(new DoctorLeave
            {
                DoctorId = model.DoctorId,
                StartDate = model.StartDate.Date,
                EndDate = model.EndDate.Date,
                Reason = model.Reason
            });

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Leave added for Dr. {doctor.User?.FullName}.";
            return RedirectToAction(nameof(DoctorLeaves));
        }

        // POST /Secretary/DeleteLeave/{id} — Delete leave record
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteLeave(int id)
        {
            var leave = await _db.DoctorLeaves
                .Include(l => l.Doctor!)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(l => l.LeaveId == id);

            if (leave == null)
            {
                TempData["ErrorMessage"] = "Leave record not found.";
                return RedirectToAction(nameof(DoctorLeaves));
            }

            var doctorName = leave.Doctor?.User?.FullName ?? "Doctor";
            _db.DoctorLeaves.Remove(leave);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Leave deleted for Dr. {doctorName}.";
            return RedirectToAction(nameof(DoctorLeaves));
        }

        // ─── Helpers ───────────────────────────────────────────────────────────────
        private async Task<bool> PopulateRescheduleContextAsync(RescheduleAppointmentViewModel model)
        {
            var appt = await _db.Appointments
                .Include(a => a.Customer!.User)
                .Include(a => a.Doctor!.User)
                .Include(a => a.Service)
                .FirstOrDefaultAsync(a => a.AppointmentId == model.AppointmentId);

            if (appt == null) return false;

            model.DoctorId = appt.DoctorId;
            model.PatientName = appt.Customer?.User?.FullName ?? "-";
            model.DoctorName = appt.Doctor?.User?.FullName ?? "-";
            model.ServiceName = appt.Service?.Name ?? "-";
            model.CurrentAppointmentDate = appt.AppointmentDate;

            if (model.NewDate == default)
                model.NewDate = appt.AppointmentDate.Date;

            if (string.IsNullOrWhiteSpace(model.NewTime))
                model.NewTime = appt.AppointmentDate.ToString("HH:mm");

            var slots = await _bookingService.GetAvailableSlotsAsync(appt.DoctorId, model.NewDate.Date);
            model.AvailableSlots = slots
                .Select(s => s.ToString("HH:mm"))
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            // Keep current slot in dropdown for same date.
            if (model.NewDate.Date == appt.AppointmentDate.Date)
            {
                var current = appt.AppointmentDate.ToString("HH:mm");
                if (!model.AvailableSlots.Contains(current))
                    model.AvailableSlots.Add(current);
            }

            model.AvailableSlots = model.AvailableSlots.OrderBy(s => s).ToList();
            return true;
        }

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
