using System.Net;

namespace DentalClinic.IntegrationTests;

/// <summary>
/// Tests authentication and role-based authorization at the HTTP layer.
/// Covers every protected route across all three roles.
/// </summary>
public class AuthTests : IClassFixture<DentalClinicFactory>
{
    private readonly HttpClient _client;

    public AuthTests(DentalClinicFactory factory)
    {
        _client = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    // ─── Unauthenticated access → 401 ────────────────────────────────────────

    [Theory]
    [InlineData("/Doctor/Dashboard")]
    [InlineData("/Secretary/Dashboard")]
    [InlineData("/Secretary/Calendar")]
    [InlineData("/Secretary/Customers")]
    [InlineData("/Secretary/Doctors")]
    [InlineData("/Secretary/Services")]
    [InlineData("/Secretary/Payments")]
    [InlineData("/Secretary/DoctorLeaves")]
    [InlineData("/Customer/Dashboard")]
    [InlineData("/Customer/PaymentHistory")]
    [InlineData("/Customer/EditProfile")]
    [InlineData("/Appointment/Book")]
    [InlineData("/Appointment/Index")]
    [InlineData("/Appointment/History")]
    [InlineData("/Notification/All")]
    [InlineData("/Notification/Unread")]
    public async Task ProtectedRoute_Unauthenticated_Returns401(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─── Secretary routes: Customer → 403 ────────────────────────────────────

    [Theory]
    [InlineData("/Secretary/Dashboard")]
    [InlineData("/Secretary/Calendar")]
    [InlineData("/Secretary/Customers")]
    [InlineData("/Secretary/Doctors")]
    [InlineData("/Secretary/Services")]
    [InlineData("/Secretary/Payments")]
    [InlineData("/Secretary/DoctorLeaves")]
    public async Task SecretaryRoute_AsCustomer_ReturnsForbidden(string path)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Add("X-Test-Role", "Customer");

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── Secretary routes: Doctor → 403 ─────────────────────────────────────

    [Theory]
    [InlineData("/Secretary/Dashboard")]
    [InlineData("/Secretary/Customers")]
    [InlineData("/Secretary/Services")]
    public async Task SecretaryRoute_AsDoctor_ReturnsForbidden(string path)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Add("X-Test-Role", "Doctor");

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── Doctor routes: Customer → 403 ───────────────────────────────────────

    [Theory]
    [InlineData("/Doctor/Dashboard")]
    public async Task DoctorRoute_AsCustomer_ReturnsForbidden(string path)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Add("X-Test-Role", "Customer");

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── Doctor routes: Secretary → 403 ──────────────────────────────────────

    [Theory]
    [InlineData("/Doctor/Dashboard")]
    public async Task DoctorRoute_AsSecretary_ReturnsForbidden(string path)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Add("X-Test-Role", "Secretary");

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── Customer routes: Doctor → 403 ───────────────────────────────────────

    [Theory]
    [InlineData("/Customer/Dashboard")]
    [InlineData("/Customer/PaymentHistory")]
    [InlineData("/Customer/EditProfile")]
    [InlineData("/Appointment/Book")]
    [InlineData("/Appointment/History")]
    [InlineData("/Notification/All")]
    public async Task CustomerRoute_AsDoctor_ReturnsForbidden(string path)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Add("X-Test-Role", "Doctor");

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── Customer routes: Secretary → 403 ────────────────────────────────────

    [Theory]
    [InlineData("/Customer/Dashboard")]
    [InlineData("/Customer/PaymentHistory")]
    [InlineData("/Customer/EditProfile")]
    [InlineData("/Notification/All")]
    public async Task CustomerRoute_AsSecretary_ReturnsForbidden(string path)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Add("X-Test-Role", "Secretary");

        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── Secretary routes: Secretary → success ───────────────────────────────

    [Theory]
    [InlineData("/Secretary/Dashboard")]
    [InlineData("/Secretary/Doctors")]
    [InlineData("/Secretary/Calendar")]
    [InlineData("/Secretary/Customers")]
    [InlineData("/Secretary/Services")]
    [InlineData("/Secretary/DoctorLeaves")]
    public async Task SecretaryRoute_AsSecretary_ReturnsSuccess(string path)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Add("X-Test-Role", "Secretary");

        var response = await _client.SendAsync(req);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── Public routes: always accessible ────────────────────────────────────

    [Theory]
    [InlineData("/")]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    [InlineData("/Account/DoctorLogin")]
    [InlineData("/Account/SecretaryLogin")]
    public async Task PublicRoute_Unauthenticated_ReturnsSuccess(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
