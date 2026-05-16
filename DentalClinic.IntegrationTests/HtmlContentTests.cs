using System.Net;

namespace DentalClinic.IntegrationTests;

/// <summary>
/// Verifies the rendered HTML content of key pages:
/// - No raw C# code leaked into output (Razor syntax bugs)
/// - No CDN dependencies (all assets served locally)
/// - Key structural elements present (titles, nav, forms)
/// </summary>
public class HtmlContentTests : IClassFixture<DentalClinicFactory>
{
    private readonly HttpClient _secretary;
    private readonly HttpClient _customer;
    private readonly HttpClient _anon;

    public HtmlContentTests(DentalClinicFactory factory)
    {
        _secretary = factory.CreateClient(new() { AllowAutoRedirect = false });
        _secretary.DefaultRequestHeaders.Add("X-Test-Role", "Secretary");

        _customer = factory.CreateClient(new() { AllowAutoRedirect = false });
        _customer.DefaultRequestHeaders.Add("X-Test-Role", "Customer");

        _anon = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    // ─── No raw C# leaked into rendered HTML ─────────────────────────────────

    [Theory]
    [InlineData("/Secretary/Dashboard")]
    [InlineData("/Secretary/Doctors")]
    [InlineData("/Secretary/Customers")]
    [InlineData("/Secretary/Services")]
    [InlineData("/Secretary/Calendar")]
    [InlineData("/Secretary/DoctorLeaves")]
    [InlineData("/Secretary/CreateAppointment")]
    public async Task SecretaryPage_DoesNotLeakRawCSharp(string path)
    {
        var response = await _secretary.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        // Razor syntax that wasn't evaluated
        Assert.DoesNotContain(".ToString(", html);
        Assert.DoesNotContain("@Html", html);
        Assert.DoesNotContain("@model", html);
        Assert.DoesNotContain("@{", html);
    }

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/DoctorLogin")]
    [InlineData("/Account/SecretaryLogin")]
    [InlineData("/Account/Register")]
    public async Task AccountPage_DoesNotLeakRawCSharp(string path)
    {
        var response = await _anon.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(".ToString(", html);
        Assert.DoesNotContain("@Html", html);
        Assert.DoesNotContain("@model", html);
    }

    // ─── No CDN dependencies ─────────────────────────────────────────────────

    [Theory]
    [InlineData("/Secretary/Dashboard")]
    [InlineData("/Account/Login")]
    [InlineData("/Account/DoctorLogin")]
    [InlineData("/Account/SecretaryLogin")]
    [InlineData("/")]
    public async Task Page_UsesLocalAssets_NotCdn(string path)
    {
        var response = await _anon.GetAsync(path);
        // For protected routes, use anon which gets 401 — those are handled separately
        if (response.StatusCode == HttpStatusCode.Unauthorized) return;

        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("cdn.jsdelivr.net", html);
        Assert.DoesNotContain("fonts.googleapis.com", html);
    }

    [Theory]
    [InlineData("/Secretary/Dashboard")]
    [InlineData("/Secretary/Doctors")]
    public async Task SecretaryPage_UsesLocalAssets_NotCdn(string path)
    {
        var response = await _secretary.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("cdn.jsdelivr.net", html);
        Assert.DoesNotContain("fonts.googleapis.com", html);
        Assert.Contains("/lib/bootstrap/", html);
        Assert.Contains("/lib/bootstrap-icons/", html);
    }

    // ─── Key page elements present ───────────────────────────────────────────

    [Theory]
    [InlineData("/Secretary/Dashboard",        "Secretary Dashboard")]
    [InlineData("/Secretary/Doctors",          "Doctors")]
    [InlineData("/Secretary/Customers",        "Patients")]
    [InlineData("/Secretary/Services",         "Services")]
    [InlineData("/Secretary/Calendar",         "Calendar")]
    [InlineData("/Secretary/CreateAppointment","Create Appointment")]
    [InlineData("/Secretary/CreateDoctor",     "Add New Doctor")]
    public async Task SecretaryPage_ContainsExpectedHeading(string path, string expectedText)
    {
        var response = await _secretary.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains(expectedText, html);
    }

    [Theory]
    [InlineData("/Account/Login",          "Patient Login")]
    [InlineData("/Account/DoctorLogin",    "Doctor Login")]
    [InlineData("/Account/SecretaryLogin", "Secretary Login")]
    [InlineData("/Account/Register",       "Create")]
    public async Task AccountPage_ContainsExpectedTitle(string path, string expectedText)
    {
        var response = await _anon.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains(expectedText, html);
    }

    // ─── Navigation correctness ───────────────────────────────────────────────

    [Fact]
    public async Task SecretaryLayout_ContainsAllNavLinks()
    {
        var response = await _secretary.GetAsync("/Secretary/Dashboard");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("/Secretary/Calendar",  html);
        Assert.Contains("/Secretary/Payments",  html);
        Assert.Contains("/Secretary/Customers", html);
        Assert.Contains("/Secretary/Doctors",   html);
        Assert.Contains("/Secretary/Services",  html);
    }

    [Fact]
    public async Task CustomerLayout_ContainsAllNavLinks()
    {
        var response = await _customer.GetAsync("/Appointment/Book");
        var html = await response.Content.ReadAsStringAsync();

        // Check link text rather than URLs — tag helpers may omit
        // the default "Index" segment in generated hrefs.
        Assert.Contains("Book Appointment",  html);
        Assert.Contains("My Appointments",   html);
        Assert.Contains("History",           html);
        Assert.Contains("Payments",          html);
    }

    // ─── Forms have antiforgery tokens ───────────────────────────────────────

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    [InlineData("/Secretary/CreateDoctor")]
    [InlineData("/Secretary/Services")]
    [InlineData("/Appointment/Book")]
    public async Task Page_FormsContainAntiForgeryToken(string path)
    {
        var response = path.StartsWith("/Account") || path.StartsWith("/Appointment/Book")
            ? await _anon.GetAsync(path)
            : await _secretary.GetAsync(path);

        if (response.StatusCode != HttpStatusCode.OK) return;

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("__RequestVerificationToken", html);
    }
}
