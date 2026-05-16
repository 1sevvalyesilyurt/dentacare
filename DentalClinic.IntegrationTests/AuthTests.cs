using System.Net;

namespace DentalClinic.IntegrationTests;

/// <summary>
/// Tests the authentication and role-based authorization at the HTTP level.
/// </summary>
public class AuthTests : IClassFixture<DentalClinicFactory>
{
    private readonly HttpClient _client;

    public AuthTests(DentalClinicFactory factory)
    {
        _client = factory.CreateClient(new()
        {
            AllowAutoRedirect = false
        });
    }

    // ─── Unauthenticated access ───────────────────────────────────────────────

    [Theory]
    [InlineData("/Doctor/Dashboard")]
    [InlineData("/Secretary/Dashboard")]
    [InlineData("/Customer/Dashboard")]
    [InlineData("/Appointment/Book")]
    [InlineData("/Appointment/Index")]
    public async Task ProtectedRoute_Unauthenticated_Returns401(string path)
    {
        // No X-Test-Role header → TestAuthHandler returns NoResult → 401
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─── Role-based access control ────────────────────────────────────────────

    [Theory]
    [InlineData("/Secretary/Dashboard")]
    [InlineData("/Secretary/Doctors")]
    [InlineData("/Secretary/Payments")]
    public async Task SecretaryRoute_AsCustomer_ReturnsForbidden(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-Role", "Customer");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/Doctor/Dashboard")]
    public async Task DoctorRoute_AsCustomer_ReturnsForbidden(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-Role", "Customer");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/Customer/Dashboard")]
    [InlineData("/Appointment/Book")]
    public async Task CustomerRoute_AsDoctor_ReturnsForbidden(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-Role", "Doctor");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/Secretary/Dashboard")]
    [InlineData("/Secretary/Doctors")]
    public async Task SecretaryRoute_AsSecretary_ReturnsSuccess(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-Role", "Secretary");

        var response = await _client.SendAsync(request);

        // 200 OK or redirect within same area — not 401/403
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── Public routes always accessible ─────────────────────────────────────

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
