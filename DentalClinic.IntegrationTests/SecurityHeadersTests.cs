using System.Net;

namespace DentalClinic.IntegrationTests;

/// <summary>
/// Verifies that security response headers are present on every response.
/// </summary>
public class SecurityHeadersTests : IClassFixture<DentalClinicFactory>
{
    private readonly HttpClient _client;

    public SecurityHeadersTests(DentalClinicFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Account/Login")]
    public async Task Response_ContainsXFrameOptions(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.True(response.Headers.Contains("X-Frame-Options"),
            $"X-Frame-Options header missing on {path}");
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").First());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Account/Login")]
    public async Task Response_ContainsXContentTypeOptions(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.True(response.Headers.Contains("X-Content-Type-Options"),
            $"X-Content-Type-Options header missing on {path}");
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Account/Login")]
    public async Task Response_ContainsContentSecurityPolicy(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.True(response.Headers.Contains("Content-Security-Policy"),
            $"Content-Security-Policy header missing on {path}");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Account/Login")]
    public async Task Response_ContainsReferrerPolicy(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.True(response.Headers.Contains("Referrer-Policy"),
            $"Referrer-Policy header missing on {path}");
    }
}
