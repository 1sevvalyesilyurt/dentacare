using System.Net;

namespace DentalClinic.IntegrationTests;

/// <summary>
/// Verifies that rate limiting returns HTTP 429 after the allowed limit is exceeded.
/// Each test uses its own factory instance to get a fresh rate limiter state.
/// </summary>
public class RateLimitTests : IClassFixture<DentalClinicFactory>
{
    private readonly DentalClinicFactory _factory;

    public RateLimitTests(DentalClinicFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LoginEndpoint_ExceedLimit_Returns429()
    {
        // Each factory.CreateClient() shares the same in-process server,
        // so we use a fresh factory to get a clean rate limiter bucket.
        await using var factory = new DentalClinicFactory();
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        HttpResponseMessage? lastResponse = null;

        // Send 11 POST requests — limit is 10 per minute per IP
        for (int i = 0; i <= 10; i++)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Email",    "x@x.com"),
                new KeyValuePair<string, string>("Password", "wrong"),
                new KeyValuePair<string, string>("__RequestVerificationToken", "fake")
            });
            lastResponse = await client.PostAsync("/Account/Login", content);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }

    [Fact]
    public async Task RegisterEndpoint_ExceedLimit_Returns429()
    {
        await using var factory = new DentalClinicFactory();
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        HttpResponseMessage? lastResponse = null;

        // Send 6 POST requests — limit is 5 per minute per IP
        for (int i = 0; i <= 5; i++)
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("Email",    "x@x.com"),
                new KeyValuePair<string, string>("Password", "wrong"),
                new KeyValuePair<string, string>("__RequestVerificationToken", "fake")
            });
            lastResponse = await client.PostAsync("/Account/Register", content);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }
}
