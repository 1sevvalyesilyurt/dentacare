using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Web.Controllers
{
    /// <summary>
    /// Dedicated dashboard for the Customer (Patient) role.
    /// Previously spread across AppointmentController — now has its own home.
    /// </summary>
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

        // GET /Customer/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            var customer = await _db.Customers
                .Include(c => c.Notifications)
                .FirstOrDefaultAsync(c => c.UserId == user!.Id);

            if (customer == null) return RedirectToAction("Register", "Account");

            var upcoming = await _db.Appointments
                .Include(a => a.Doctor!.User)
                .Include(a => a.Service)
                .Where(a => a.CustomerId == customer.CustomerId
                         && a.AppointmentDate >= DateTime.UtcNow
                         && a.Status != AppointmentStatus.Cancelled)
                .OrderBy(a => a.AppointmentDate)
                .Take(5)
                .ToListAsync();

            var pastCount = await _db.Appointments
                .CountAsync(a => a.CustomerId == customer.CustomerId
                              && a.Status == AppointmentStatus.Completed);

            var totalSpent = await _db.Payments
                .Include(p => p.Appointment)
                .Where(p => p.Appointment!.CustomerId == customer.CustomerId)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            var unreadCount = await _db.Notifications
                .CountAsync(n => n.CustomerId == customer.CustomerId && !n.IsRead);

            ViewBag.PatientName  = user!.FullName;
            ViewBag.PastCount    = pastCount;
            ViewBag.TotalSpent   = totalSpent;
            ViewBag.UnreadCount  = unreadCount;

            return View(upcoming);
        }
    }
}
