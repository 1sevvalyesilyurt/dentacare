using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using DentalClinic.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Web.Controllers
{
    /// <summary>
    /// Handles user authentication.
    /// Three separate login entry-points — one per role — each with its own view.
    /// POST is shared via the private SignInAsync helper.
    /// </summary>
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly AppDbContext _db;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            AppDbContext db,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
            _logger = logger;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // SECRETARY LOGIN  →  /Account/SecretaryLogin
        // ═══════════════════════════════════════════════════════════════════════════

        [HttpGet]
        public IActionResult SecretaryLogin()
        {
            if (_signInManager.IsSignedIn(User)) return RedirectToRoleHome();
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> SecretaryLogin(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            return await PerformLogin(model, "Secretary", nameof(SecretaryLogin));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // DOCTOR LOGIN  →  /Account/DoctorLogin
        // ═══════════════════════════════════════════════════════════════════════════

        [HttpGet]
        public IActionResult DoctorLogin()
        {
            if (_signInManager.IsSignedIn(User)) return RedirectToRoleHome();
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> DoctorLogin(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            return await PerformLogin(model, "Doctor", nameof(DoctorLogin));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // CUSTOMER (PATIENT) LOGIN  →  /Account/Login
        // ═══════════════════════════════════════════════════════════════════════════

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (_signInManager.IsSignedIn(User)) return RedirectToRoleHome();
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);
            return await PerformLogin(model, "Customer", nameof(Login), returnUrl);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // REGISTER  →  /Account/Register  (Customer only)
        // ═══════════════════════════════════════════════════════════════════════════

        [HttpGet]
        public IActionResult Register()
        {
            if (_signInManager.IsSignedIn(User)) return RedirectToRoleHome();
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        [EnableRateLimiting("register")]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = new ApplicationUser
            {
                UserName      = model.Email,
                Email         = model.Email,
                FullName      = model.FullName,
                PhoneNumber   = model.PhoneNumber,
                DateOfBirth   = model.DateOfBirth,
                IsActive      = true,
                CreatedAt     = DateTime.UtcNow,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Customer");

                // Create Customer profile record
                var customer = new Customer { UserId = user.Id };
                _db.Customers.Add(customer);
                await _db.SaveChangesAsync();

                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Dashboard", "Customer");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // LOGOUT
        // ═══════════════════════════════════════════════════════════════════════════

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════════════

        private async Task<IActionResult> PerformLogin(
            LoginViewModel model,
            string expectedRole,
            string viewName,
            string? returnUrl = null)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null || !user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Invalid credentials.");
                return View(viewName, model);
            }

            // Role guard: prevent a doctor logging in via the secretary portal, etc.
            if (!await _userManager.IsInRoleAsync(user, expectedRole))
            {
                ModelState.AddModelError(string.Empty,
                    $"This portal is for {expectedRole}s only. Please use the correct login page.");
                return View(viewName, model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("{Role} {Email} logged in.", expectedRole, model.Email);
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToRoleHome();
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Account locked. Try again in 5 minutes.");
                return View(viewName, model);
            }

            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(viewName, model);
        }

        private IActionResult RedirectToRoleHome()
        {
            if (User.IsInRole("Secretary"))
                return RedirectToAction("Dashboard", "Secretary");
            if (User.IsInRole("Doctor"))
                return RedirectToAction("Dashboard", "Doctor");
            return RedirectToAction("Dashboard", "Customer");
        }
    }
}
