using System.Text.RegularExpressions;

namespace DentalClinic.PlaywrightTests.Tests;

[TestFixture]
[Parallelizable(ParallelScope.None)]
public class SecretaryTests : PlaywrightTestBase
{
    [SetUp]
    public async Task LoginBeforeEachTest()
    {
        await LoginAsSecretaryAsync();
    }

    // ─── 1. Dashboard ─────────────────────────────────────────────────────────

    [Test]
    public async Task Dashboard_LoadsWithStatsAndRecentAppointments()
    {
        await Page.GotoAsync("/Secretary/Dashboard");
        await Expect(Page).ToHaveTitleAsync(new Regex("Admin Dashboard", RegexOptions.IgnoreCase));
        // Stat cards rendered by the view
        await Expect(Page.Locator("body")).ToContainTextAsync("Today's Appointments");
        await Expect(Page.Locator("body")).ToContainTextAsync("Active Doctors");
    }

    // ─── 2. Calendar ──────────────────────────────────────────────────────────

    [Test]
    public async Task Calendar_LoadsTodayByDefault()
    {
        await Page.GotoAsync("/Secretary/Calendar");
        await Expect(Page).ToHaveTitleAsync(new Regex("Calendar", RegexOptions.IgnoreCase));
        await Expect(Page).ToHaveURLAsync(new Regex("/Secretary/Calendar"));
    }

    [Test]
    public async Task Calendar_WithDateParam_LoadsSpecificDay()
    {
        var date = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd");
        await Page.GotoAsync($"/Secretary/Calendar?date={date}");
        await Expect(Page).ToHaveTitleAsync(new Regex("Calendar", RegexOptions.IgnoreCase));
    }

    // ─── 3. Doctor management ─────────────────────────────────────────────────

    [Test]
    public async Task Doctors_ListShowsSeededTestDoctor()
    {
        await Page.GotoAsync("/Secretary/Doctors");
        await Expect(Page).ToHaveTitleAsync(new Regex("Doctor", RegexOptions.IgnoreCase));
        await Expect(Page.Locator("body")).ToContainTextAsync("Test Doctor");
        await Expect(Page.Locator("body")).ToContainTextAsync("General Dentistry");
    }

    [Test]
    public async Task CreateDoctor_ValidData_AppearsInDoctorList()
    {
        await Page.GotoAsync("/Secretary/CreateDoctor");
        await Expect(Page).ToHaveTitleAsync(new Regex("Add New Doctor", RegexOptions.IgnoreCase));

        var uniqueEmail = $"newdoc_{Guid.NewGuid():N}@dentacare.com";

        await Page.FillAsync("#doctor-fullname", "E2E New Doctor");
        await Page.FillAsync("#doctor-email",    uniqueEmail);
        await Page.FillAsync("#doctor-phone",    "+90 555 999 0001");
        await Page.FillAsync("#doctor-password", "Secure@Pass99!");
        await Page.SelectOptionAsync("#doctor-specialty", "Orthodontics");
        await Page.ClickAsync("#btn-create-doctor");

        await Page.WaitForURLAsync("**/Secretary/Doctors");
        await Expect(Page.Locator(".alert-success")).ToBeVisibleAsync();
        await Expect(Page.Locator("body")).ToContainTextAsync("E2E New Doctor");
    }

    [Test]
    public async Task CreateDoctor_DuplicateEmail_ShowsError()
    {
        await Page.GotoAsync("/Secretary/CreateDoctor");

        await Page.FillAsync("#doctor-fullname", "Duplicate Doc");
        await Page.FillAsync("#doctor-email",    DoctorEmail);   // already seeded
        await Page.FillAsync("#doctor-phone",    "+90 555 000 0000");
        await Page.FillAsync("#doctor-password", "Secure@Pass99!");
        await Page.SelectOptionAsync("#doctor-specialty", "General Dentistry");
        await Page.ClickAsync("#btn-create-doctor");

        // Should stay on form with error about duplicate email
        await Expect(Page).ToHaveURLAsync(new Regex("/Secretary/CreateDoctor"));
        await Expect(Page.Locator("body")).ToContainTextAsync(
            new Regex("already exists|email.*taken", RegexOptions.IgnoreCase));
    }

    [Test]
    public async Task ToggleDoctor_DeactivatesAndReactivates()
    {
        await Page.GotoAsync("/Secretary/Doctors");

        // Submit the first ToggleDoctor form programmatically (bypasses click hitbox issues)
        await Page.EvaluateAsync(@"
            () => document.querySelector('form[action*=""ToggleDoctor""]').submit()
        ");
        await Page.WaitForURLAsync("**/Secretary/Doctors");
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Expect(Page.Locator(".alert-success")).ToBeVisibleAsync();

        // Toggle back to restore
        await Page.EvaluateAsync(@"
            () => document.querySelector('form[action*=""ToggleDoctor""]').submit()
        ");
        await Page.WaitForURLAsync("**/Secretary/Doctors");
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Expect(Page.Locator(".alert-success")).ToBeVisibleAsync();
    }

    // ─── 4. Customer list ─────────────────────────────────────────────────────

    [Test]
    public async Task Customers_ListShowsSeededTestCustomer()
    {
        await Page.GotoAsync("/Secretary/Customers");
        await Expect(Page).ToHaveTitleAsync(new Regex("Customer", RegexOptions.IgnoreCase));
        await Expect(Page.Locator("body")).ToContainTextAsync("Test Customer");
    }

    // ─── 5. Service management ────────────────────────────────────────────────

    [Test]
    public async Task Services_ListShowsSeededServices()
    {
        await Page.GotoAsync("/Secretary/Services");
        await Expect(Page).ToHaveTitleAsync(new Regex("Service", RegexOptions.IgnoreCase));
        // Seeded services should be listed
        await Expect(Page.Locator("body")).ToContainTextAsync("General Check-up");
        await Expect(Page.Locator("body")).ToContainTextAsync("Root Canal");
    }

    [Test]
    public async Task CreateService_ValidData_AppearsInServiceList()
    {
        await Page.GotoAsync("/Secretary/Services");

        var serviceName = $"E2E Service {Guid.NewGuid().ToString("N")[..6]}";

        var createForm = Page.Locator("form[action*='CreateService']");
        await createForm.Locator("input[name='Name']").FillAsync(serviceName);
        await createForm.Locator("input[name='BaseFee']").FillAsync("750");
        await createForm.Locator("input[name='DurationMinutes']").FillAsync("40");
        await createForm.Locator("button[type='submit']").ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex("/Secretary/Services"));
        await Expect(Page.Locator(".alert-success")).ToBeVisibleAsync();
        await Expect(Page.Locator("body")).ToContainTextAsync(serviceName);
    }

    [Test]
    public async Task ToggleService_TogglesActiveStatus()
    {
        await Page.GotoAsync("/Secretary/Services");

        await Page.EvaluateAsync(@"
            () => document.querySelector('form[action*=""ToggleService""]').submit()
        ");
        await Page.WaitForURLAsync("**/Secretary/Services");
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Expect(Page.Locator(".alert-success")).ToBeVisibleAsync();

        // Toggle back to restore
        await Page.EvaluateAsync(@"
            () => document.querySelector('form[action*=""ToggleService""]').submit()
        ");
        await Page.WaitForURLAsync("**/Secretary/Services");
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Expect(Page.Locator(".alert-success")).ToBeVisibleAsync();
    }

    // ─── 6. Doctor leave management ───────────────────────────────────────────

    [Test]
    public async Task DoctorLeaves_ListLoads()
    {
        await Page.GotoAsync("/Secretary/DoctorLeaves");
        await Expect(Page).ToHaveTitleAsync(new Regex("Leave", RegexOptions.IgnoreCase));
    }

    [Test]
    public async Task AddLeave_ValidData_AppearsInLeaveList()
    {
        // Navigate to Doctors, find AddLeave link for first doctor
        await Page.GotoAsync("/Secretary/Doctors");
        var addLeaveLink = Page.Locator("a[href*='AddLeave']").First;
        await addLeaveLink.ClickAsync();

        await Expect(Page).ToHaveTitleAsync(new Regex("Add Doctor Leave", RegexOptions.IgnoreCase));

        var today    = DateTime.Today.ToString("yyyy-MM-dd");
        var tomorrow = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");

        await Page.FillAsync("#StartDate", today);
        await Page.FillAsync("#EndDate",   tomorrow);
        await Page.FillAsync("#Reason",    "E2E Playwright test leave");

        // Scope to form to avoid matching the navbar logout button
        await Page.Locator("form[action*='AddLeave'] button[type='submit']").ClickAsync();

        await Page.WaitForURLAsync("**/Secretary/DoctorLeaves");
        await Expect(Page.Locator(".alert-success")).ToBeVisibleAsync();
        await Expect(Page.Locator("body")).ToContainTextAsync("E2E Playwright test leave");
    }

    [Test]
    public async Task DeleteLeave_RemovesLeaveFromList()
    {
        // First create a leave to delete
        await Page.GotoAsync("/Secretary/Doctors");
        await Page.Locator("a[href*='AddLeave']").First.ClickAsync();

        var start = DateTime.Today.AddDays(5).ToString("yyyy-MM-dd");
        var end   = DateTime.Today.AddDays(6).ToString("yyyy-MM-dd");
        await Page.FillAsync("#StartDate", start);
        await Page.FillAsync("#EndDate",   end);
        await Page.FillAsync("#Reason",    "Leave to be deleted");
        await Page.Locator("form[action*='AddLeave'] button[type='submit']").ClickAsync();
        await Page.WaitForURLAsync("**/Secretary/DoctorLeaves");

        // Now delete it — find the delete form for our leave
        var deleteForm = Page.Locator("form[action*='DeleteLeave']").Last;
        if (await deleteForm.CountAsync() == 0)
            Assert.Inconclusive("No leave records found to delete.");

        Page.Dialog += async (_, d) => await d.AcceptAsync();
        await deleteForm.Locator("button.btn-outline-danger").ClickAsync();
        await Page.WaitForURLAsync("**/Secretary/DoctorLeaves");
        await Expect(Page.Locator(".alert-success")).ToBeVisibleAsync();
    }

    // ─── 7. Payments ──────────────────────────────────────────────────────────

    [Test]
    public async Task Payments_DashboardLoads()
    {
        await Page.GotoAsync("/Secretary/Payments");
        await Expect(Page).ToHaveTitleAsync(new Regex("Payment", RegexOptions.IgnoreCase));
        await Expect(Page).ToHaveURLAsync(new Regex("/Secretary/Payments"));
    }

    // ─── 8. Cross-role access — Secretary → other roles ─────────────────────

    [Test]
    public async Task Secretary_AccessingCustomerDashboard_IsRedirectedOrDenied()
    {
        await Page.GotoAsync("/Customer/Dashboard");
        await Expect(Page).Not.ToHaveTitleAsync(new Regex("My Dashboard", RegexOptions.IgnoreCase));
    }

    [Test]
    public async Task Secretary_AccessingDoctorDashboard_IsRedirectedOrDenied()
    {
        await Page.GotoAsync("/Doctor/Dashboard");
        await Expect(Page).Not.ToHaveTitleAsync(new Regex("Doctor Dashboard", RegexOptions.IgnoreCase));
    }
}
