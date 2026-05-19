using System.Text.RegularExpressions;

namespace DentalClinic.PlaywrightTests.Tests;

[TestFixture]
[Parallelizable(ParallelScope.None)]
public class DoctorTests : PlaywrightTestBase
{
    [SetUp]
    public async Task LoginBeforeEachTest()
    {
        await LoginAsDoctorAsync();
    }

    // ─── 1. Dashboard ─────────────────────────────────────────────────────────

    [Test]
    public async Task Dashboard_LoadsAndShowsDoctorInfo()
    {
        await Page.GotoAsync("/Doctor/Dashboard");
        await Expect(Page).ToHaveTitleAsync(new Regex("Doctor Dashboard", RegexOptions.IgnoreCase));
        await Expect(Page.Locator("body")).ToContainTextAsync("Test Doctor");
        await Expect(Page.Locator("body")).ToContainTextAsync("General Dentistry");
    }

    [Test]
    public async Task Dashboard_WithDateParam_LoadsSpecificDay()
    {
        var specificDate = DateTime.Today.AddDays(3).ToString("yyyy-MM-dd");
        await Page.GotoAsync($"/Doctor/Dashboard?date={specificDate}");
        await Expect(Page).ToHaveTitleAsync(new Regex("Doctor Dashboard", RegexOptions.IgnoreCase));
        // Page should still load correctly with a date parameter
        await Expect(Page.Locator("body")).ToContainTextAsync("Test Doctor");
    }

    [Test]
    public async Task Dashboard_ShowsTodayScheduleSection()
    {
        await Page.GotoAsync("/Doctor/Dashboard");
        // Should have a section for today's appointments (even if empty)
        await Expect(Page.Locator("body")).ToContainTextAsync(
            new Regex("today|schedule|appointment", RegexOptions.IgnoreCase));
    }

    // ─── 2. Cross-role access — Doctor → other roles ──────────────────────────

    [Test]
    public async Task Doctor_AccessingSecretaryDashboard_IsRedirectedOrDenied()
    {
        await Page.GotoAsync("/Secretary/Dashboard");
        await Expect(Page).Not.ToHaveTitleAsync(new Regex("Admin Dashboard", RegexOptions.IgnoreCase));
    }

    [Test]
    public async Task Doctor_AccessingCustomerDashboard_IsRedirectedOrDenied()
    {
        await Page.GotoAsync("/Customer/Dashboard");
        await Expect(Page).Not.ToHaveTitleAsync(new Regex("My Dashboard", RegexOptions.IgnoreCase));
    }

    [Test]
    public async Task Doctor_AccessingAppointmentBook_IsRedirectedOrDenied()
    {
        await Page.GotoAsync("/Appointment/Book");
        // Customer-only route — should be denied for Doctor
        await Expect(Page).Not.ToHaveTitleAsync(new Regex("^Book", RegexOptions.IgnoreCase));
    }
}
