using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace DentalClinic.PlaywrightTests.Tests;

[TestFixture]
[Parallelizable(ParallelScope.None)]
public class AuthTests : PlaywrightTestBase
{
    // ─── 1. Anonymous home page ───────────────────────────────────────────────

    [Test]
    public async Task HomePage_Anonymous_ShowsTitleAndPortalLinks()
    {
        await Page.GotoAsync("/");
        await Expect(Page).ToHaveTitleAsync(new Regex("DentaCare", RegexOptions.IgnoreCase));

        // Should have at least one login link visible
        var loginLinks = Page.Locator("a[href*='Login']");
        await Expect(loginLinks.First).ToBeVisibleAsync();
    }

    // ─── 2. Customer registration ─────────────────────────────────────────────

    [Test]
    public async Task Register_ValidData_CreatesAccountAndRedirectsToDashboard()
    {
        var email = $"e2e_{Guid.NewGuid():N}@test.com";

        await Page.GotoAsync("/Account/Register");
        await Expect(Page).ToHaveTitleAsync(new Regex("Create Account", RegexOptions.IgnoreCase));

        await Page.FillAsync("#reg-fullname",         "E2E Test User");
        await Page.FillAsync("#reg-email",            email);
        await Page.FillAsync("#reg-phone",            "+90 555 000 0001");
        await Page.FillAsync("#reg-dob",              "1990-01-15");
        await Page.FillAsync("#reg-password",         "TestPass@123!");
        await Page.FillAsync("#reg-confirm-password", "TestPass@123!");
        await Page.ClickAsync("#btn-register");

        await Page.WaitForURLAsync("**/Customer/Dashboard");
        await Expect(Page).ToHaveTitleAsync(new Regex("Dashboard", RegexOptions.IgnoreCase));
    }

    [Test]
    public async Task Register_DuplicateEmail_ShowsError()
    {
        await Page.GotoAsync("/Account/Register");

        // Use the pre-seeded customer account — it already exists
        await Page.FillAsync("#reg-fullname",         "Duplicate User");
        await Page.FillAsync("#reg-email",            CustomerEmail);
        await Page.FillAsync("#reg-phone",            "+90 555 000 0002");
        await Page.FillAsync("#reg-dob",              "1985-06-20");
        await Page.FillAsync("#reg-password",         "TestPass@123!");
        await Page.FillAsync("#reg-confirm-password", "TestPass@123!");
        await Page.ClickAsync("#btn-register");

        // Should stay on register page with an error
        await Expect(Page).ToHaveURLAsync(new Regex("/Account/Register"));
        var error = Page.Locator(".alert-danger, [asp-validation-summary]");
        await Expect(error.First).ToBeVisibleAsync();
    }

    // ─── 3. Customer login / logout ───────────────────────────────────────────

    [Test]
    public async Task CustomerLogin_ValidCredentials_RedirectsToDashboard()
    {
        await LoginAsCustomerAsync();
        await Expect(Page).ToHaveTitleAsync(new Regex("Dashboard", RegexOptions.IgnoreCase));
    }

    [Test]
    public async Task CustomerLogin_InvalidPassword_ShowsError()
    {
        await Page.GotoAsync("/Account/Login");
        await Page.FillAsync("#pat-email",    CustomerEmail);
        await Page.FillAsync("#pat-password", "WrongPassword99!");
        await Page.ClickAsync("#btn-pat-login");

        await Expect(Page).ToHaveURLAsync(new Regex("/Account/Login"));
        await Expect(Page.Locator(".alert-danger, .validation-summary-errors")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Logout_AfterLogin_RedirectsToHome()
    {
        await LoginAsCustomerAsync();

        // Open user dropdown (Bootstrap) then click logout
        await Page.ClickAsync("#userDropdown");
        await Page.Locator("form[action*='Logout'] button").ClickAsync();
        await Page.WaitForURLAsync(new Regex("/(Home/Index)?$"));
    }

    // ─── 4. Role guard — wrong portal rejection ───────────────────────────────

    [Test]
    public async Task CustomerTriesSecretaryPortal_ShowsRoleError()
    {
        await Page.GotoAsync("/Account/SecretaryLogin");
        await Page.FillAsync("#sec-email",    CustomerEmail);
        await Page.FillAsync("#sec-password", CustomerPassword);
        await Page.ClickAsync("#btn-sec-login");

        // Must stay on SecretaryLogin with an error about wrong portal
        await Expect(Page).ToHaveURLAsync(new Regex("/Account/SecretaryLogin"));
        await Expect(Page.Locator(".alert-danger, .validation-summary-errors")).ToBeVisibleAsync();
    }

    [Test]
    public async Task SecretaryTriesCustomerPortal_ShowsRoleError()
    {
        await Page.GotoAsync("/Account/Login");
        await Page.FillAsync("#pat-email",    SecretaryEmail);
        await Page.FillAsync("#pat-password", SecretaryPassword);
        await Page.ClickAsync("#btn-pat-login");

        await Expect(Page).ToHaveURLAsync(new Regex("/Account/Login"));
        await Expect(Page.Locator(".alert-danger, .validation-summary-errors")).ToBeVisibleAsync();
    }

    // ─── 5. Secretary login ───────────────────────────────────────────────────

    [Test]
    public async Task SecretaryLogin_ValidCredentials_RedirectsToDashboard()
    {
        await LoginAsSecretaryAsync();
        await Expect(Page).ToHaveTitleAsync(new Regex("Admin Dashboard", RegexOptions.IgnoreCase));
    }

    // ─── 6. Doctor login ──────────────────────────────────────────────────────

    [Test]
    public async Task DoctorLogin_ValidCredentials_RedirectsToDashboard()
    {
        await LoginAsDoctorAsync();
        await Expect(Page).ToHaveTitleAsync(new Regex("Doctor Dashboard", RegexOptions.IgnoreCase));
    }

    // ─── 7. Unauthenticated access to protected pages ─────────────────────────

    [Test]
    public async Task UnauthenticatedAccess_AppointmentIndex_RedirectsToLogin()
    {
        await Page.GotoAsync("/Appointment");
        await Expect(Page).ToHaveURLAsync(new Regex("/Account/Login"));
    }

    [Test]
    public async Task UnauthenticatedAccess_SecretaryDashboard_RedirectsToLogin()
    {
        await Page.GotoAsync("/Secretary/Dashboard");
        // Redirect should go to login (any login page)
        await Expect(Page).ToHaveURLAsync(new Regex("/Account/Login"));
    }

    // ─── 8. Already-signed-in redirect ───────────────────────────────────────

    [Test]
    public async Task AlreadySignedIn_CustomerVisitsLoginPage_RedirectsToDashboard()
    {
        await LoginAsCustomerAsync();

        // Navigate back to login — should be redirected away since already signed in
        await Page.GotoAsync("/Account/Login");
        await Expect(Page).ToHaveURLAsync(new Regex("/Customer/Dashboard"));
    }

    // ─── 9. Register validation ───────────────────────────────────────────────

    [Test]
    public async Task Register_EmptyForm_ShowsValidationErrors()
    {
        await Page.GotoAsync("/Account/Register");
        // Submit without filling anything
        await Page.ClickAsync("#btn-register");
        // Should stay on Register with validation errors
        await Expect(Page).ToHaveURLAsync(new Regex("/Account/Register"));
        await Expect(Page.Locator(".text-danger, .field-validation-error").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Register_PasswordMismatch_ShowsError()
    {
        await Page.GotoAsync("/Account/Register");
        await Page.FillAsync("#reg-fullname",         "Mismatch User");
        await Page.FillAsync("#reg-email",            $"mismatch_{Guid.NewGuid().ToString("N")}@test.com");
        await Page.FillAsync("#reg-phone",            "+90 555 000 0099");
        await Page.FillAsync("#reg-dob",              "1995-03-10");
        await Page.FillAsync("#reg-password",         "Password@123!");
        await Page.FillAsync("#reg-confirm-password", "DifferentPass@456!");
        await Page.ClickAsync("#btn-register");
        // Must stay on register and show a validation error (client-side or server-side)
        await Expect(Page).ToHaveURLAsync(new Regex("/Account/Register"));
        await Expect(Page.Locator("body")).ToContainTextAsync(
            new Regex("match|confirm|password", RegexOptions.IgnoreCase));
    }

    // ─── 10. Home page ────────────────────────────────────────────────────────

    [Test]
    public async Task Home_PrivacyPage_LoadsSuccessfully()
    {
        await Page.GotoAsync("/Home/Privacy");
        await Expect(Page).ToHaveTitleAsync(new Regex("Privacy", RegexOptions.IgnoreCase));
    }

    // ─── 11. Account lockout ─────────────────────────────────────────────────
    // Uses a freshly-registered throwaway account so the shared testcustomer
    // remains unlocked for other test fixtures.

    [Test]
    public async Task Login_LocksOutAfterRepeatedFailures()
    {
        // 1. Register a one-off account
        var lockoutEmail = $"lockout_{Guid.NewGuid():N}@test.com";
        await Page.GotoAsync("/Account/Register");
        await Page.FillAsync("#reg-fullname",         "Lockout Test User");
        await Page.FillAsync("#reg-email",            lockoutEmail);
        await Page.FillAsync("#reg-phone",            "+90 555 000 0088");
        await Page.FillAsync("#reg-dob",              "1988-07-15");
        await Page.FillAsync("#reg-password",         "Correct@Pass99!");
        await Page.FillAsync("#reg-confirm-password", "Correct@Pass99!");
        await Page.ClickAsync("#btn-register");
        await Page.WaitForURLAsync("**/Customer/Dashboard");

        // 2. Log out
        await Page.ClickAsync("#userDropdown");
        await Page.Locator("form[action*='Logout'] button").ClickAsync();
        await Page.WaitForURLAsync(new Regex("/(Home/Index)?$"));

        // 3. Attempt login 5 times with wrong password (MaxFailedAccessAttempts = 5)
        for (int i = 0; i < 5; i++)
        {
            await Page.GotoAsync("/Account/Login");
            await Page.FillAsync("#pat-email",    lockoutEmail);
            await Page.FillAsync("#pat-password", "WrongPass!!99");
            await Page.ClickAsync("#btn-pat-login");
        }

        // 4. One more attempt should now show the lockout message
        await Page.GotoAsync("/Account/Login");
        await Page.FillAsync("#pat-email",    lockoutEmail);
        await Page.FillAsync("#pat-password", "WrongPass!!99");
        await Page.ClickAsync("#btn-pat-login");

        await Expect(Page.Locator(".alert-danger, .validation-summary-errors"))
            .ToContainTextAsync(new Regex("lock", RegexOptions.IgnoreCase));
    }
}
