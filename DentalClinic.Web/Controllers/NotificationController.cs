using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Web.Controllers
{
    /// <summary>
    /// API-style controller that returns unread notification count (for navbar badge)
    /// and marks notifications as read.
    /// </summary>
    [Authorize(Roles = "Customer")]
    public class NotificationController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationController(AppDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // GET /Notification/Unread — Returns JSON count for navbar badge
        [HttpGet]
        public async Task<IActionResult> Unread()
        {
            var user = await _userManager.GetUserAsync(User);
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.UserId == user!.Id);
            if (customer == null) return Json(new { count = 0 });

            var count = await _db.Notifications
                .CountAsync(n => n.CustomerId == customer.CustomerId && !n.IsRead);

            return Json(new { count });
        }

        // GET /Notification/All — Full notification list page
        public async Task<IActionResult> All()
        {
            var user = await _userManager.GetUserAsync(User);
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.UserId == user!.Id);
            if (customer == null) return RedirectToAction("Index", "Appointment");

            var notifications = await _db.Notifications
                .Where(n => n.CustomerId == customer.CustomerId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            // Mark all as read on view
            notifications.Where(n => !n.IsRead).ToList().ForEach(n => n.IsRead = true);
            await _db.SaveChangesAsync();

            return View(notifications);
        }
    }
}
