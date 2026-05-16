using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Web.Controllers
{
    [Authorize(Roles = "Customer")]
    public class CustomerController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public CustomerController(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        private CancellationToken ct => HttpContext.RequestAborted;

        // GET /Customer/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var user     = await _userManager.GetUserAsync(User);
            var customer = await _db.Customers
                .Include(c => c.Notifications)
                .FirstOrDefaultAsync(c => c.UserId == user!.Id, ct);

            if (customer == null) return RedirectToAction("Register", "Account");

            var upcoming = await _db.Appointments
                .Include(a => a.Doctor!.User)
                .Include(a => a.Service)
                .Where(a => a.CustomerId == customer.CustomerId
                         && a.AppointmentDate >= DateTime.UtcNow
                         && a.Status != AppointmentStatus.Cancelled)
                .OrderBy(a => a.AppointmentDate)
                .Take(5)
                .ToListAsync(ct);

            var pastCount  = await _db.Appointments
                .CountAsync(a => a.CustomerId == customer.CustomerId
                              && a.Status == AppointmentStatus.Completed, ct);

            var totalSpent = await _db.Payments
                .Include(p => p.Appointment)
                .Where(p => p.Appointment!.CustomerId == customer.CustomerId)
                .SumAsync(p => (decimal?)p.Amount, ct) ?? 0;

            var unreadCount = await _db.Notifications
                .CountAsync(n => n.CustomerId == customer.CustomerId && !n.IsRead, ct);

            ViewBag.PatientName  = user!.FullName;
            ViewBag.PastCount    = pastCount;
            ViewBag.TotalSpent   = totalSpent;
            ViewBag.UnreadCount  = unreadCount;

            return View(upcoming);
        }

        // GET /Customer/PaymentHistory (UC-C08)
        public async Task<IActionResult> PaymentHistory()
        {
            var user     = await _userManager.GetUserAsync(User);
            var customer = await _db.Customers
                .FirstOrDefaultAsync(c => c.UserId == user!.Id, ct);

            if (customer == null) return RedirectToAction("Register", "Account");

            var payments = await _db.Payments
                .Include(p => p.Appointment)
                    .ThenInclude(a => a!.Doctor!.User)
                .Include(p => p.Appointment)
                    .ThenInclude(a => a!.Service)
                .Where(p => p.Appointment!.CustomerId == customer.CustomerId)
                .OrderByDescending(p => p.PaidAt)
                .ToListAsync(ct);

            ViewBag.TotalSpent  = payments.Sum(p => p.Amount);
            ViewBag.PatientName = user!.FullName;

            return View(payments);
        }

        // GET /Customer/EditProfile (UC-C09)
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            return View(new ViewModels.EditProfileViewModel
            {
                FullName    = user.FullName,
                PhoneNumber = user.PhoneNumber,
                DateOfBirth = user.DateOfBirth
            });
        }

        // POST /Customer/EditProfile (UC-C09)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(ViewModels.EditProfileViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            bool wantsPasswordChange = !string.IsNullOrWhiteSpace(model.CurrentPassword)
                                    || !string.IsNullOrWhiteSpace(model.NewPassword)
                                    || !string.IsNullOrWhiteSpace(model.ConfirmNewPassword);

            // 1. Password change first — fail fast before touching profile
            if (wantsPasswordChange)
            {
                var pwResult = await _userManager.ChangePasswordAsync(
                    user, model.CurrentPassword!, model.NewPassword!);

                if (!pwResult.Succeeded)
                {
                    foreach (var e in pwResult.Errors)
                        ModelState.AddModelError(nameof(model.CurrentPassword), e.Description);
                    return View(model);
                }
            }

            // 2. Update personal info
            user.FullName    = model.FullName;
            user.PhoneNumber = model.PhoneNumber;
            user.DateOfBirth = model.DateOfBirth;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var e in updateResult.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                return View(model);
            }

            TempData["SuccessMessage"] = "Your profile has been updated successfully.";
            return RedirectToAction(nameof(Dashboard));
        }
    }
}
