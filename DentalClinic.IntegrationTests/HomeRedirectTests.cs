using System.Net;

namespace DentalClinic.IntegrationTests;

/// <summary>
/// Verifies that HomeController.Index redirects authenticated users to their
/// role-specific dashboard, and shows the portal selection page to anonymous visitors.
/// </summary>
public class HomeRedirectTests : IClassFixture<DentalClinicFactory>
{
    private readonly DentalClinicFactory _factory;

    public HomeRedirectTests(DentalClinicFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HomeIndex_Unauthenticated_Returns200PortalPage()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HomeIndex_AsCustomer_RedirectsToCustomerDashboard()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Role", "Customer");

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Customer/Dashboard", response.Headers.Location?.ToString() ?? "");
    }

    [Fact]
    public async Task HomeIndex_AsSecretary_RedirectsToSecretaryDashboard()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Role", "Secretary");

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Secretary/Dashboard", response.Headers.Location?.ToString() ?? "");
    }

    [Fact]
    public async Task HomeIndex_AsDoctor_RedirectsToDoctorDashboard()
    {
        var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-Role", "Doctor");

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/Doctor/Dashboard", response.Headers.Location?.ToString() ?? "");
    }
}
