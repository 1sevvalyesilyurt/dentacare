using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using DentalClinic.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
    options.Password.RequiredLength         = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase       = false;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(5);
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
    options.ExpireTimeSpan    = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// ─── Application Services ─────────────────────────────────────────────────
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// ─── Background Service (Reminder Notifications - UC-SYS01) ───────────────
builder.Services.AddHostedService<ReminderBackgroundService>();

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
app.UseStaticFiles();
app.UseRouting();

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

        await SeedDataAsync(services);
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
static async Task SeedDataAsync(IServiceProvider services)
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
        var secretary = new ApplicationUser
        {
            UserName   = secretaryEmail,
            Email      = secretaryEmail,
            FullName   = "Admin Secretary",
            IsActive   = true,
            CreatedAt  = DateTime.UtcNow,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(secretary, "Admin@123");
        await userManager.AddToRoleAsync(secretary, "Secretary");
    }

    // Seed default Doctor account
    const string doctorEmail = "doctor@dentacare.com";
    if (await userManager.FindByEmailAsync(doctorEmail) == null)
    {
        var docUser = new ApplicationUser
        {
            UserName   = doctorEmail,
            Email      = doctorEmail,
            FullName   = "John Doe",
            IsActive   = true,
            CreatedAt  = DateTime.UtcNow,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(docUser, "Doctor@123");
        await userManager.AddToRoleAsync(docUser, "Doctor");

        var doctorProfile = new DentalClinic.Web.Models.Doctor
        {
            UserId = docUser.Id,
            Specialty = "General Dentistry",
            CommissionRate = 0.70m,
            WorkingHoursStart = new TimeSpan(9, 0, 0),
            WorkingHoursEnd = new TimeSpan(17, 0, 0),
            SlotDurationMinutes = 30,
            IsActive = true
        };
        db.Doctors.Add(doctorProfile);
        await db.SaveChangesAsync();
    }

    // Seed default Customer account
    const string customerEmail = "customer@dentacare.com";
    if (await userManager.FindByEmailAsync(customerEmail) == null)
    {
        var custUser = new ApplicationUser
        {
            UserName   = customerEmail,
            Email      = customerEmail,
            FullName   = "Jane Smith",
            IsActive   = true,
            CreatedAt  = DateTime.UtcNow,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(custUser, "Customer@123");
        await userManager.AddToRoleAsync(custUser, "Customer");

        var customerProfile = new DentalClinic.Web.Models.Customer
        {
            UserId = custUser.Id
        };
        db.Customers.Add(customerProfile);
        await db.SaveChangesAsync();
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
