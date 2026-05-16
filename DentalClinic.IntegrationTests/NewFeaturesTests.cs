using System.Net;

namespace DentalClinic.IntegrationTests;

/// <summary>
/// Integration tests for features added by Dev1 and Dev2:
/// UC-C08 Payment History, UC-C09 Edit Profile, UC-S04 Create Appointment,
/// UC-S05 Reschedule Appointment.
/// </summary>
public class NewFeaturesTests : IClassFixture<DentalClinicFactory>
{
    private readonly HttpClient _customer;
    private readonly HttpClient _secretary;

    public NewFeaturesTests(DentalClinicFactory factory)
    {
        _customer  = factory.CreateClient(new() { AllowAutoRedirect = false });
        _customer.DefaultRequestHeaders.Add("X-Test-Role", "Customer");

        _secretary = factory.CreateClient(new() { AllowAutoRedirect = false });
        _secretary.DefaultRequestHeaders.Add("X-Test-Role", "Secretary");
    }

    // ─── UC-C08: Payment History ─────────────────────────────────────────────

    [Fact]
    public async Task PaymentHistory_AsCustomer_NotForbidden()
    {
        // Auth passes (not 401/403); 500 is acceptable without real DB user in test env
        var response = await _customer.GetAsync("/Customer/PaymentHistory");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PaymentHistory_Unauthenticated_Returns401()
    {
        await using var factory = new DentalClinicFactory();
        var anon = factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await anon.GetAsync("/Customer/PaymentHistory");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PaymentHistory_AsDoctor_Returns403()
    {
        await using var factory = new DentalClinicFactory();
        var doctorClient = factory.CreateClient(new() { AllowAutoRedirect = false });
        doctorClient.DefaultRequestHeaders.Add("X-Test-Role", "Doctor");

        var response = await doctorClient.GetAsync("/Customer/PaymentHistory");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── UC-C09: Edit Profile ────────────────────────────────────────────────

    [Fact]
    public async Task EditProfile_GET_AsCustomer_NotForbidden()
    {
        var response = await _customer.GetAsync("/Customer/EditProfile");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EditProfile_AsDoctor_Returns403()
    {
        await using var factory = new DentalClinicFactory();
        var doctorClient = factory.CreateClient(new() { AllowAutoRedirect = false });
        doctorClient.DefaultRequestHeaders.Add("X-Test-Role", "Doctor");

        var response = await doctorClient.GetAsync("/Customer/EditProfile");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── UC-S04: Create Appointment (view) ───────────────────────────────────

    [Fact]
    public async Task CreateAppointment_GET_AsSecretary_NotForbidden()
    {
        var response = await _secretary.GetAsync("/Secretary/CreateAppointment");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateAppointment_AsCustomer_Returns403()
    {
        var response = await _customer.GetAsync("/Secretary/CreateAppointment");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── UC-S05: Reschedule Appointment (view) ───────────────────────────────

    [Fact]
    public async Task RescheduleAppointment_GET_AsSecretary_NotForbidden()
    {
        // Auth passes — 404 or 500 (no real DB user) both acceptable, but never 401/403
        var response = await _secretary.GetAsync("/Secretary/RescheduleAppointment/99999");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RescheduleAppointment_AsCustomer_Returns403()
    {
        var response = await _customer.GetAsync("/Secretary/RescheduleAppointment/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── UC-S03: Calendar view ───────────────────────────────────────────────

    [Fact]
    public async Task Calendar_AsSecretary_NotForbidden()
    {
        var response = await _secretary.GetAsync("/Secretary/Calendar");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── UC-S10: Services CRUD ───────────────────────────────────────────────

    [Fact]
    public async Task Services_AsSecretary_NotForbidden()
    {
        var response = await _secretary.GetAsync("/Secretary/Services");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
