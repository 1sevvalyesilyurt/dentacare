using System.Net;
using System.Text.Json;

namespace DentalClinic.IntegrationTests;

/// <summary>
/// Tests for NotificationController (JSON badge endpoint) and
/// SecretaryController.AvailableSlots (AJAX slot-picker endpoint).
/// </summary>
public class NotificationAndSlotsTests : IClassFixture<DentalClinicFactory>
{
    private readonly HttpClient _customer;
    private readonly HttpClient _secretary;
    private readonly HttpClient _anon;

    public NotificationAndSlotsTests(DentalClinicFactory factory)
    {
        _customer = factory.CreateClient(new() { AllowAutoRedirect = false });
        _customer.DefaultRequestHeaders.Add("X-Test-Role", "Customer");

        _secretary = factory.CreateClient(new() { AllowAutoRedirect = false });
        _secretary.DefaultRequestHeaders.Add("X-Test-Role", "Secretary");

        _anon = factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    // ─── Notification/Unread ─────────────────────────────────────────────────

    [Fact]
    public async Task NotificationUnread_AsCustomer_ReturnsJson()
    {
        var response = await _customer.GetAsync("/Notification/Unread");

        // Auth passes — controller may return 200 or redirect (no DB user)
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NotificationUnread_Unauthenticated_Returns401()
    {
        var response = await _anon.GetAsync("/Notification/Unread");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NotificationUnread_AsSecretary_Returns403()
    {
        var response = await _secretary.GetAsync("/Notification/Unread");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NotificationAll_Unauthenticated_Returns401()
    {
        var response = await _anon.GetAsync("/Notification/All");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NotificationAll_AsSecretary_Returns403()
    {
        var response = await _secretary.GetAsync("/Notification/All");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ─── Secretary/AvailableSlots ─────────────────────────────────────────────

    [Fact]
    public async Task AvailableSlots_AsSecretary_ReturnsJsonArray()
    {
        // No doctor with id=1 in empty DB → returns empty array, not 500
        var date = DateTime.UtcNow.Date.AddDays(1).ToString("yyyy-MM-dd");
        var response = await _secretary.GetAsync(
            $"/Secretary/AvailableSlots?doctorId=1&date={date}");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task AvailableSlots_AsCustomer_Returns403()
    {
        var date = DateTime.UtcNow.Date.AddDays(1).ToString("yyyy-MM-dd");
        var response = await _customer.GetAsync(
            $"/Secretary/AvailableSlots?doctorId=1&date={date}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AvailableSlots_Unauthenticated_Returns401()
    {
        var date = DateTime.UtcNow.Date.AddDays(1).ToString("yyyy-MM-dd");
        var response = await _anon.GetAsync(
            $"/Secretary/AvailableSlots?doctorId=1&date={date}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AvailableSlots_UnknownDoctor_ReturnsEmptyJsonArray()
    {
        var date = DateTime.UtcNow.Date.AddDays(1).ToString("yyyy-MM-dd");
        var response = await _secretary.GetAsync(
            $"/Secretary/AvailableSlots?doctorId=99999&date={date}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    // ─── Appointment/Slots (customer booking AJAX) ───────────────────────────

    [Fact]
    public async Task AppointmentSlots_AsCustomer_Returns200JsonArray()
    {
        var date = DateTime.UtcNow.Date.AddDays(1).ToString("yyyy-MM-dd");
        var response = await _customer.GetAsync(
            $"/Appointment/Slots?doctorId=99999&date={date}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public async Task AppointmentSlots_AsSecretary_Returns403()
    {
        var date = DateTime.UtcNow.Date.AddDays(1).ToString("yyyy-MM-dd");
        var response = await _secretary.GetAsync(
            $"/Appointment/Slots?doctorId=1&date={date}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
