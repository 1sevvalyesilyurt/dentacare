using System.Net;

namespace DentalClinic.IntegrationTests;

/// <summary>
/// Integration tests for features added by Dev1 and Dev2:
/// UC-C08 Payment History, UC-C09 Edit Profile, UC-S04 Create Appointment,
/// UC-S05 Reschedule Appointment.
///
/// All tests reuse the shared factory — no per-test factories needed here
/// (rate limiting is not involved in these flows).
/// </summary>
public class NewFeaturesTests : IClassFixture<DentalClinicFactory>
{
    private readonly HttpClient _customer;
    private readonly HttpClient _secretary;
    private readonly HttpClient _doctor;
    private readonly HttpClient _anon;

    public NewFeaturesTests(DentalClinicFactory factory)
    {
        _customer = factory.CreateClient(new() { AllowAutoRedirect = false });
        _customer.DefaultRequestHeaders.Add("X-Test-Role", "Customer");

        _secretary = factory.CreateClient(new() { AllowAutoRedirect = false });
        _secretary.DefaultRequestHeaders.Add("X-Test-Role", "Secretary");

        _doctor = factory.CreateClient(new() { AllowAutoRedirect = false });
        _doctor.DefaultRequestHeaders.Add("X-Test-Role", "Doctor");

        _anon = factory.CreateClient(new() { AllowAutoRedirect = false });
        // No role header → unauthenticated
    }

    // ─── UC-C08: Payment History ─────────────────────────────────────────────

    [Fact]
    public async Task PaymentHistory_AsCustomer_NotForbidden()
    {
        var response = await _customer.GetAsync("/Customer/PaymentHistory");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PaymentHistory_Unauthenticated_Returns401()
    {
        var response = await _anon.GetAsync("/Customer/PaymentHistory");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PaymentHistory_AsDoctor_Returns403()
    {
        var response = await _doctor.GetAsync("/Customer/PaymentHistory");

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
        var response = await _doctor.GetAsync("/Customer/EditProfile");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EditProfile_AsSecretary_Returns403()
    {
        var response = await _secretary.GetAsync("/Customer/EditProfile");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── UC-S04: Create Appointment ─────────────────────────────────────────

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

    // ─── UC-S05: Reschedule Appointment ─────────────────────────────────────

    [Fact]
    public async Task RescheduleAppointment_GET_AsSecretary_NotForbidden()
    {
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

    // ─── UC-S03: Calendar ────────────────────────────────────────────────────

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
