using System.Net;
using System.Text.RegularExpressions;
using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DentalClinic.IntegrationTests;

/// <summary>
/// Integration tests for AccountController: login validation, wrong-portal
/// detection, and registration flow.
///
/// Tests that need a pre-existing user seed via UserManager directly (no HTTP)
/// to bypass the per-minute rate limiter on the Register endpoint.
/// Registration-specific tests use isolated factory instances.
/// </summary>
public class AccountControllerTests : IClassFixture<DentalClinicFactory>
{
    private readonly DentalClinicFactory _factory;

    public AccountControllerTests(DentalClinicFactory factory)
    {
        _factory = factory;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string ExtractToken(string html)
    {
        // Razor generates: name="__RequestVerificationToken" type="hidden" value="..."
        var m = Regex.Match(html,
            @"__RequestVerificationToken[^>]+value=""([^""]+)""");
        return m.Success ? m.Groups[1].Value : "";
    }

    private async Task<(HttpClient client, string token)> Fresh(string url,
        DentalClinicFactory? factory = null)
    {
        var client = (factory ?? _factory).CreateClient(
            new() { AllowAutoRedirect = false });
        var html = await (await client.GetAsync(url)).Content.ReadAsStringAsync();
        return (client, ExtractToken(html));
    }

    /// Seeds a Customer user directly via UserManager — no HTTP, no rate limiting.
    private async Task SeedCustomer(string email, string password = "Test@Pass1!")
    {
        using var scope = _factory.Services.CreateScope();
        var sp          = scope.ServiceProvider;
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var db          = sp.GetRequiredService<AppDbContext>();

        if (!await roleManager.RoleExistsAsync("Customer"))
            await roleManager.CreateAsync(new IdentityRole("Customer"));

        if (await userManager.FindByEmailAsync(email) != null) return;

        var user = new ApplicationUser
        {
            UserName       = email,
            Email          = email,
            FullName       = "Seeded User",
            IsActive       = true,
            CreatedAt      = DateTime.UtcNow,
            EmailConfirmed = true,
        };
        await userManager.CreateAsync(user, password);
        await userManager.AddToRoleAsync(user, "Customer");

        db.Customers.Add(new Customer { UserId = user.Id });
        await db.SaveChangesAsync();
    }

    // ─── Login: invalid / missing credentials ─────────────────────────────────

    [Fact]
    public async Task Login_NonExistentEmail_Returns200WithError()
    {
        var (client, token) = await Fresh("/Account/Login");

        var response = await client.PostAsync("/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"]                      = "nobody@example.com",
                ["Password"]                   = "WrongPass1!",
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Invalid", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_EmptyEmail_Returns200()
    {
        var (client, token) = await Fresh("/Account/Login");

        var response = await client.PostAsync("/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"]                      = "",
                ["Password"]                   = "Test@Pass1!",
                ["__RequestVerificationToken"] = token,
            }));

        // ModelState invalid → re-renders form (200) or 400 depending on validation
        Assert.True(response.StatusCode is HttpStatusCode.OK
                                        or HttpStatusCode.BadRequest);
    }

    // ─── Login: wrong portal ──────────────────────────────────────────────────

    [Fact]
    public async Task Login_CustomerViaSecretaryPortal_Returns200WithPortalError()
    {
        const string email = "wrong-portal-test@test.com";
        await SeedCustomer(email);

        var (client, token) = await Fresh("/Account/SecretaryLogin");

        var response = await client.PostAsync("/Account/SecretaryLogin",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"]                      = email,
                ["Password"]                   = "Test@Pass1!",
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // "This portal is for Secretarys only."
        Assert.Contains("Secretary", await response.Content.ReadAsStringAsync());
    }

    // ─── Login: valid credentials ─────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCustomer_RedirectsToCustomerDashboard()
    {
        const string email    = "login-valid@test.com";
        const string password = "Test@Pass1!";
        await SeedCustomer(email, password);

        var (client, token) = await Fresh("/Account/Login");

        var response = await client.PostAsync("/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"]                      = email,
                ["Password"]                   = password,
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Customer/Dashboard",
            response.Headers.Location?.ToString() ?? "");
    }

    [Fact]
    public async Task Login_ValidCustomer_WrongPassword_Returns200WithError()
    {
        const string email = "wrong-pw@test.com";
        await SeedCustomer(email);

        var (client, token) = await Fresh("/Account/Login");

        var response = await client.PostAsync("/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Email"]                      = email,
                ["Password"]                   = "TotallyWrong99!",
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Invalid", await response.Content.ReadAsStringAsync());
    }

    // ─── Registration: each test uses a fresh isolated factory ───────────────
    // (avoids hitting the 5-req/min rate limit on the shared factory)

    [Fact]
    public async Task Register_ValidData_RedirectsToCustomerDashboard()
    {
        await using var factory = new DentalClinicFactory();
        var (client, token) = await Fresh("/Account/Register", factory);

        var response = await client.PostAsync("/Account/Register",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["FullName"]                   = "New Patient",
                ["Email"]                      = "reg-ok@test.com",
                ["PhoneNumber"]                = "+905001234567",
                ["Password"]                   = "Test@Pass1!",
                ["ConfirmPassword"]            = "Test@Pass1!",
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Customer/Dashboard",
            response.Headers.Location?.ToString() ?? "");
    }

    [Fact]
    public async Task Register_WeakPassword_Returns200()
    {
        await using var factory = new DentalClinicFactory();
        var (client, token) = await Fresh("/Account/Register", factory);

        var response = await client.PostAsync("/Account/Register",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["FullName"]                   = "Weak Pass",
                ["Email"]                      = "weakpw@test.com",
                ["PhoneNumber"]                = "+905001234567",
                ["Password"]                   = "weak",        // too short, no digit/special
                ["ConfirmPassword"]            = "weak",
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_MismatchedPasswords_Returns200()
    {
        await using var factory = new DentalClinicFactory();
        var (client, token) = await Fresh("/Account/Register", factory);

        var response = await client.PostAsync("/Account/Register",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["FullName"]                   = "Mismatch",
                ["Email"]                      = "mismatch@test.com",
                ["PhoneNumber"]                = "+905001234567",
                ["Password"]                   = "Test@Pass1!",
                ["ConfirmPassword"]            = "Other@Pass2!",
                ["__RequestVerificationToken"] = token,
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns200()
    {
        await using var factory = new DentalClinicFactory();

        // First registration — succeeds
        var (client1, token1) = await Fresh("/Account/Register", factory);
        await client1.PostAsync("/Account/Register",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["FullName"]                   = "First",
                ["Email"]                      = "dup@test.com",
                ["PhoneNumber"]                = "+905001234567",
                ["Password"]                   = "Test@Pass1!",
                ["ConfirmPassword"]            = "Test@Pass1!",
                ["__RequestVerificationToken"] = token1,
            }));

        // Second registration with same email — should fail with 200 + error
        var (client2, token2) = await Fresh("/Account/Register", factory);
        var response = await client2.PostAsync("/Account/Register",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["FullName"]                   = "Second",
                ["Email"]                      = "dup@test.com",
                ["PhoneNumber"]                = "+905001234567",
                ["Password"]                   = "Test@Pass1!",
                ["ConfirmPassword"]            = "Test@Pass1!",
                ["__RequestVerificationToken"] = token2,
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
