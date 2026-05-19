using System.Text.RegularExpressions;

namespace DentalClinic.PlaywrightTests.Tests;

[TestFixture]
[Parallelizable(ParallelScope.None)]
public class CustomerTests : PlaywrightTestBase
{
    [SetUp]
    public async Task LoginBeforeEachTest()
    {
        await LoginAsCustomerAsync();
    }

    // ─── 1. Dashboard ─────────────────────────────────────────────────────────

    [Test]
    public async Task Dashboard_LoadsAndShowsPatientName()
    {
        await Page.GotoAsync("/Customer/Dashboard");
        await Expect(Page).ToHaveTitleAsync(new Regex("Dashboard", RegexOptions.IgnoreCase));
        // Page should contain the seeded customer's name
        await Expect(Page.Locator("body")).ToContainTextAsync("Test Customer");
    }

    [Test]
    public async Task Dashboard_HasBookAppointmentLink()
    {
        await Page.GotoAsync("/Customer/Dashboard");
        var bookLink = Page.Locator("a[href*='/Appointment/Book']").First;
        await Expect(bookLink).ToBeVisibleAsync();
    }

    // ─── 2. Payment history ───────────────────────────────────────────────────

    [Test]
    public async Task PaymentHistory_LoadsWithoutError()
    {
        await Page.GotoAsync("/Customer/PaymentHistory");
        await Expect(Page).ToHaveTitleAsync(new Regex("Payment History", RegexOptions.IgnoreCase));
        await Expect(Page).ToHaveURLAsync(new Regex("/Customer/PaymentHistory"));
    }

    // ─── 3. Edit profile — GET ────────────────────────────────────────────────

    [Test]
    public async Task EditProfile_GET_PreloadsFullNameField()
    {
        await Page.GotoAsync("/Customer/EditProfile");
        await Expect(Page).ToHaveTitleAsync(new Regex("Edit Profile", RegexOptions.IgnoreCase));

        // Full name field should be pre-populated with the seeded customer name
        var fullNameInput = Page.Locator("#FullName");
        await Expect(fullNameInput).ToBeVisibleAsync();
        await Expect(fullNameInput).ToHaveValueAsync("Test Customer");
    }

    // ─── 4. Edit profile — POST success ──────────────────────────────────────

    [Test]
    public async Task EditProfile_POST_UpdatesNameAndShowsSuccessMessage()
    {
        await Page.GotoAsync("/Customer/EditProfile");

        // Update full name
        await Page.FillAsync("#FullName", "Updated Test Customer");
        await Page.FillAsync("#PhoneNumber", "+90 555 111 2233");

        await Page.Locator("#profile-form button[type='submit']").ClickAsync();
        await Page.WaitForURLAsync("**/Customer/Dashboard");

        // Success flash should be visible
        await Expect(Page.Locator(".alert-success")).ToBeVisibleAsync();

        // Restore the original name so subsequent tests see "Test Customer"
        await Page.GotoAsync("/Customer/EditProfile");
        await Page.FillAsync("#FullName", "Test Customer");
        await Page.Locator("#profile-form button[type='submit']").ClickAsync();
        await Page.WaitForURLAsync("**/Customer/Dashboard");
    }

    // ─── 5. Edit profile — wrong password ────────────────────────────────────

    [Test]
    public async Task EditProfile_POST_WrongCurrentPassword_ShowsError()
    {
        await Page.GotoAsync("/Customer/EditProfile");

        await Page.FillAsync("#FullName", "Test Customer");
        await Page.FillAsync("#currentPwInput", "WrongOldPassword!");
        await Page.FillAsync("#newPwInput",     "NewSecurePass@99!");
        await Page.FillAsync("#confirmPwInput", "NewSecurePass@99!");

        await Page.Locator("#profile-form button[type='submit']").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

        // Wrong password → Identity rejects and returns view with error
        await Expect(Page).ToHaveURLAsync(new Regex("/Customer/EditProfile"));
        // The field-level error span should contain the Identity error message
        await Expect(Page.Locator(".field-validation-error, .validation-summary-errors").First)
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 8_000 });
    }

    // ─── 6. Cross-role access — Customer → Secretary routes ──────────────────

    [Test]
    public async Task Customer_AccessingSecretaryDashboard_IsRedirectedOrDenied()
    {
        await Page.GotoAsync("/Secretary/Dashboard");
        // Must NOT land on the Secretary Dashboard — either redirected or denied
        await Expect(Page).Not.ToHaveTitleAsync(new Regex("Admin Dashboard", RegexOptions.IgnoreCase));
    }

    [Test]
    public async Task Customer_AccessingDoctorDashboard_IsRedirectedOrDenied()
    {
        await Page.GotoAsync("/Doctor/Dashboard");
        await Expect(Page).Not.ToHaveTitleAsync(new Regex("Doctor Dashboard", RegexOptions.IgnoreCase));
    }
}
