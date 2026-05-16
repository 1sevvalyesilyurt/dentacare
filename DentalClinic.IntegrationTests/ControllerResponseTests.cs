using System.Net;

namespace DentalClinic.IntegrationTests;

/// <summary>
/// Verifies that every page returns the correct HTTP status when accessed
/// with the right role. Secretary pages are expected to return 200 (they only
/// query the DB, which is empty but valid in tests). Customer/Doctor pages
/// redirect or return 500 when the Identity user doesn't exist in the InMemory
/// DB — we verify they at least pass authorization (not 401/403).
/// </summary>
public class ControllerResponseTests : IClassFixture<DentalClinicFactory>
{
    private readonly HttpClient _secretary;
    private readonly HttpClient _customer;
    private readonly HttpClient _doctor;

    public ControllerResponseTests(DentalClinicFactory factory)
    {
        _secretary = factory.CreateClient(new() { AllowAutoRedirect = false });
        _secretary.DefaultRequestHeaders.Add("X-Test-Role", "Secretary");

        _customer = factory.CreateClient(new() { AllowAutoRedirect = false });
        _customer.DefaultRequestHeaders.Add("X-Test-Role", "Customer");

        _doctor = factory.CreateClient(new() { AllowAutoRedirect = false });
        _doctor.DefaultRequestHeaders.Add("X-Test-Role", "Doctor");
    }

    // ─── Secretary pages: no user identity required → must return 200 ────────

    [Theory]
    [InlineData("/Secretary/Dashboard")]
    [InlineData("/Secretary/Calendar")]
    [InlineData("/Secretary/Customers")]
    [InlineData("/Secretary/Doctors")]
    [InlineData("/Secretary/Services")]
    [InlineData("/Secretary/DoctorLeaves")]
    [InlineData("/Secretary/CreateDoctor")]
    [InlineData("/Secretary/CreateAppointment")]
    public async Task SecretaryPage_AsSecretary_Returns200(string path)
    {
        var response = await _secretary.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Secretary Payments uses a ViewModel with stats — still no user needed
    [Fact]
    public async Task SecretaryPayments_AsSecretary_Returns200()
    {
        var response = await _secretary.GetAsync("/Secretary/Payments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ─── Customer pages: Identity user needed → at minimum passes auth ────────

    [Theory]
    [InlineData("/Customer/Dashboard")]
    [InlineData("/Customer/PaymentHistory")]
    [InlineData("/Customer/EditProfile")]
    [InlineData("/Appointment/Book")]
    [InlineData("/Appointment/Index")]
    [InlineData("/Appointment/History")]
    [InlineData("/Notification/All")]
    public async Task CustomerPage_AsCustomer_PassesAuthorization(string path)
    {
        var response = await _customer.GetAsync(path);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Notification/Unread returns JSON
    [Fact]
    public async Task NotificationUnread_AsCustomer_PassesAuthorization()
    {
        var response = await _customer.GetAsync("/Notification/Unread");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── Doctor pages: passes auth layer (role check), may 403 from controller ─

    [Fact]
    public async Task DoctorDashboard_AsDoctor_PassesAuthLayer()
    {
        var response = await _doctor.GetAsync("/Doctor/Dashboard");

        // Authorization middleware passes (role is "Doctor") — 401 would mean the
        // auth layer rejected the request. The controller itself calls Forbid() when
        // there is no Doctor record in the test DB (empty InMemory), which also yields
        // 403 but for a different reason. We can only verify the auth layer here.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─── Appointment/Book: customer role, no DB user → passes auth ───────────

    [Fact]
    public async Task AppointmentBook_AsCustomer_Returns200()
    {
        var response = await _customer.GetAsync("/Appointment/Book");

        // Book page only queries available doctors/services from DB (empty list OK)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
