using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace DentalClinic.PlaywrightTests;

/// <summary>
/// Base class for all Playwright E2E tests.
/// Configures BaseURL and provides shared login helpers.
/// Each test gets a fresh browser context and page (PageTest default).
/// </summary>
public abstract class PlaywrightTestBase : PageTest
{
    // ─── Seeded accounts (created by Playwright environment seed) ───────────
    protected const string SecretaryEmail    = "secretary@dentacare.com";
    protected const string SecretaryPassword = "Admin@123";
    protected const string DoctorEmail       = "testdoctor@dentacare.com";
    protected const string DoctorPassword    = "Doctor@123!";
    protected const string CustomerEmail     = "testcustomer@test.com";
    protected const string CustomerPassword  = "Customer@123!";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL           = ServerFixture.BaseUrl,
        IgnoreHTTPSErrors = true,
        // Record traces in CI for failed tests (PLAYWRIGHT_TRACE=on)
        RecordVideoDir    = null,
    };

    // Start tracing before each test so failures produce a downloadable trace
    [SetUp]
    public async Task StartTracingAsync()
    {
        await Context.Tracing.StartAsync(new()
        {
            Screenshots = true,
            Snapshots   = true,
            Sources     = true,
        });
    }

    // Stop tracing and save only on failure; discard on success to save space
    [TearDown]
    public async Task StopTracingAsync()
    {
        var testFailed = TestContext.CurrentContext.Result.Outcome.Status
            == NUnit.Framework.Interfaces.TestStatus.Failed;

        var tracePath = testFailed
            ? Path.Combine("playwright-traces",
                $"{TestContext.CurrentContext.Test.FullName.Replace('/', '_')}.zip")
            : null;

        if (tracePath != null)
            Directory.CreateDirectory("playwright-traces");

        await Context.Tracing.StopAsync(new() { Path = tracePath });
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    protected async Task LoginAsCustomerAsync()
    {
        await Page.GotoAsync("/Account/Login");
        await Page.FillAsync("#pat-email",    CustomerEmail);
        await Page.FillAsync("#pat-password", CustomerPassword);
        await Page.ClickAsync("#btn-pat-login");
        await Page.WaitForURLAsync("**/Customer/Dashboard");
    }

    protected async Task LoginAsSecretaryAsync()
    {
        await Page.GotoAsync("/Account/SecretaryLogin");
        await Page.FillAsync("#sec-email",    SecretaryEmail);
        await Page.FillAsync("#sec-password", SecretaryPassword);
        await Page.ClickAsync("#btn-sec-login");
        await Page.WaitForURLAsync("**/Secretary/Dashboard");
    }

    protected async Task LoginAsDoctorAsync()
    {
        await Page.GotoAsync("/Account/DoctorLogin");
        await Page.FillAsync("#doc-email",    DoctorEmail);
        await Page.FillAsync("#doc-password", DoctorPassword);
        await Page.ClickAsync("#btn-doc-login");
        await Page.WaitForURLAsync("**/Doctor/Dashboard");
    }

    /// <summary>Returns the next upcoming weekday (Mon-Fri) in yyyy-MM-dd format.</summary>
    protected static string NextWeekday()
    {
        var date = DateTime.Today.AddDays(1);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            date = date.AddDays(1);
        return date.ToString("yyyy-MM-dd");
    }
}
