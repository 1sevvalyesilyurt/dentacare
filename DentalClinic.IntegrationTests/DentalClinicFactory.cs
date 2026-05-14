using DentalClinic.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace DentalClinic.IntegrationTests;

/// <summary>
/// Shared WebApplicationFactory — replaces SQLite with InMemory DB so tests
/// don't touch the filesystem and can run in parallel.
/// </summary>
public class DentalClinicFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Replace SQLite with InMemory
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("IntegrationTestDb_" + Guid.NewGuid()));

            // Add fake auth scheme and make it the default so the TestAuthHandler
            // takes priority over the Identity cookie scheme for all auth decisions.
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

            // Force "Test" as the effective default — Identity's AddIdentity() sets
            // cookies as the default; we override that here so our handler wins.
            services.Configure<AuthenticationOptions>(opts =>
            {
                opts.DefaultAuthenticateScheme = "Test";
                opts.DefaultChallengeScheme    = "Test";
                opts.DefaultForbidScheme       = "Test";
                opts.DefaultScheme             = "Test";
                opts.DefaultSignInScheme       = "Test";
                opts.DefaultSignOutScheme      = "Test";
            });

            // Also suppress cookie redirect-to-login / redirect-to-access-denied
            // so any remaining cookie handling returns raw status codes.
            foreach (var scheme in new[]
            {
                IdentityConstants.ApplicationScheme,
                IdentityConstants.ExternalScheme,
                IdentityConstants.TwoFactorUserIdScheme
            })
            {
                services.PostConfigure<CookieAuthenticationOptions>(scheme, opts =>
                {
                    opts.Events.OnRedirectToLogin = ctx =>
                    {
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    };
                    opts.Events.OnRedirectToAccessDenied = ctx =>
                    {
                        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    };
                });
            }
        });
    }
}

/// <summary>
/// Test auth handler: reads X-Test-Role and X-Test-UserId request headers
/// and builds a ClaimsPrincipal, letting tests impersonate any role.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Role", out var roleValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        var role   = roleValues.ToString();
        var userId = Request.Headers.TryGetValue("X-Test-UserId", out var uid)
            ? uid.ToString() : Guid.NewGuid().ToString();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, $"Test {role}"),
            new Claim(ClaimTypes.Email, $"test-{role.ToLower()}@test.com"),
            new Claim(ClaimTypes.Role, role)
        };

        var identity  = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
