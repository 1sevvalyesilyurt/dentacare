using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using DentalClinic.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════════
// 1. Services Registration
// ═══════════════════════════════════════════════════════════════════════════

// ─── Entity Framework Core + SQLite (cross-platform) ────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─── ASP.NET Core Identity ────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit           = true;
    options.Password.RequiredLength         = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase       = true;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ─── Cookie configuration ─────────────────────────────────────────────────
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath         = "/Account/Login";
    options.LogoutPath        = "/Account/Logout";
    options.AccessDeniedPath  = "/Account/AccessDenied";
    options.ExpireTimeSpan    = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// ─── Application Services ─────────────────────────────────────────────────
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// ─── Background Service (Reminder Notifications - UC-SYS01) ───────────────
builder.Services.AddHostedService<ReminderBackgroundService>();

// ─── Rate Limiting ────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    // Login endpoint: max 10 attempts per minute per IP
    options.AddFixedWindowLimiter("login", o =>
    {
        o.Window            = TimeSpan.FromMinutes(1);
        o.PermitLimit       = 10;
        o.QueueLimit        = 0;
        o.AutoReplenishment = true;
    });

    // Register endpoint: max 5 per minute per IP
    options.AddFixedWindowLimiter("register", o =>
    {
        o.Window            = TimeSpan.FromMinutes(1);
        o.PermitLimit       = 5;
        o.QueueLimit        = 0;
        o.AutoReplenishment = true;
    });

    options.RejectionStatusCode = 429;
});

// ─── MVC ──────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ═══════════════════════════════════════════════════════════════════════════
// 2. App Pipeline Configuration
// ═══════════════════════════════════════════════════════════════════════════
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// ─── Security Headers ─────────────────────────────────────────────────────
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    context.Response.Headers.Append(
        "Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self';");
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication(); // Must come before UseAuthorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ═══════════════════════════════════════════════════════════════════════════
// 3. Database Migration + Seed (on startup)
// ═══════════════════════════════════════════════════════════════════════════
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        db.Database.Migrate(); // Apply any pending EF migrations

        await SeedDataAsync(services, builder.Configuration);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during database migration/seeding.");
    }
}

app.Run();

// ═══════════════════════════════════════════════════════════════════════════
// Seed Roles + Default Secretary Account
// ═══════════════════════════════════════════════════════════════════════════
static async Task SeedDataAsync(IServiceProvider services, IConfiguration configuration)
{
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var db          = services.GetRequiredService<AppDbContext>();

    // Ensure roles exist
    string[] roles = { "Secretary", "Doctor", "Customer" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // Seed default Secretary account
    const string secretaryEmail = "secretary@dentacare.com";
    if (await userManager.FindByEmailAsync(secretaryEmail) == null)
    {
        var secretaryPassword = configuration["SeedSettings:DefaultSecretaryPassword"]
            ?? throw new InvalidOperationException("SeedSettings:DefaultSecretaryPassword is not configured.");

        var secretary = new ApplicationUser
        {
            UserName   = secretaryEmail,
            Email      = secretaryEmail,
            FullName   = "Admin Secretary",
            IsActive   = true,
            CreatedAt  = DateTime.UtcNow,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(secretary, secretaryPassword);
        await userManager.AddToRoleAsync(secretary, "Secretary");
    }

    // Seed default Services if none exist
    if (!db.Services.Any())
    {
        db.Services.AddRange(
            new DentalClinic.Web.Models.Service { Name = "General Check-up",   BaseFee = 200,  DurationMinutes = 20 },
            new DentalClinic.Web.Models.Service { Name = "Teeth Cleaning",     BaseFee = 350,  DurationMinutes = 30 },
            new DentalClinic.Web.Models.Service { Name = "Tooth Filling",      BaseFee = 600,  DurationMinutes = 30 },
            new DentalClinic.Web.Models.Service { Name = "Root Canal",         BaseFee = 1500, DurationMinutes = 60 },
            new DentalClinic.Web.Models.Service { Name = "Tooth Extraction",   BaseFee = 500,  DurationMinutes = 30 },
            new DentalClinic.Web.Models.Service { Name = "Teeth Whitening",    BaseFee = 1200, DurationMinutes = 45 },
            new DentalClinic.Web.Models.Service { Name = "Orthodontic Consult",BaseFee = 300,  DurationMinutes = 30 },
            new DentalClinic.Web.Models.Service { Name = "Dental Implant Consult", BaseFee = 500, DurationMinutes = 45 }
        );
        await db.SaveChangesAsync();
    }
}
