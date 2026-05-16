using DentalClinic.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DentalClinic.Tests;

public class EmailServiceTests
{
    private static EmailService BuildService(Dictionary<string, string?> config)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        return new EmailService(configuration, NullLogger<EmailService>.Instance);
    }

    [Fact]
    public async Task SendAsync_SmtpNotConfigured_DoesNotThrow()
    {
        var service = BuildService(new()
        {
            ["SmtpSettings:Host"]     = "",
            ["SmtpSettings:Username"] = ""
        });

        // Should log a warning and return silently — not throw
        var ex = await Record.ExceptionAsync(() =>
            service.SendAsync("patient@test.com", "Test Subject", "<p>Hello</p>"));

        Assert.Null(ex);
    }

    [Fact]
    public async Task SendAsync_SmtpHostMissing_DoesNotThrow()
    {
        var service = BuildService(new()
        {
            // No SmtpSettings at all
        });

        var ex = await Record.ExceptionAsync(() =>
            service.SendAsync("patient@test.com", "Reminder", "<p>Hi</p>"));

        Assert.Null(ex);
    }
}
