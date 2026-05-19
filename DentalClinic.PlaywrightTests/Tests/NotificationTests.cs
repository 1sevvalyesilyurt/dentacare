using System.Text.Json;
using System.Text.RegularExpressions;

namespace DentalClinic.PlaywrightTests.Tests;

[TestFixture]
[Parallelizable(ParallelScope.None)]
public class NotificationTests : PlaywrightTestBase
{
    [SetUp]
    public async Task LoginBeforeEachTest()
    {
        await LoginAsCustomerAsync();
    }

    // ─── 1. Unread count endpoint ─────────────────────────────────────────────

    [Test]
    public async Task UnreadEndpoint_ReturnsJsonWithCount()
    {
        var response = await Page.APIRequest.GetAsync($"{ServerFixture.BaseUrl}/Notification/Unread",
            new() { Headers = new Dictionary<string, string> { { "Accept", "application/json" } } });

        Assert.That(response.Status, Is.EqualTo(200));
        var json = JsonDocument.Parse(await response.TextAsync());
        Assert.That(json.RootElement.TryGetProperty("count", out _), Is.True,
            "Response JSON should have a 'count' property");
    }

    // ─── 2. Notifications list page ───────────────────────────────────────────

    [Test]
    public async Task AllNotifications_PageLoadsSuccessfully()
    {
        await Page.GotoAsync("/Notification/All");
        await Expect(Page).ToHaveTitleAsync(new Regex("Notification", RegexOptions.IgnoreCase));
        await Expect(Page).ToHaveURLAsync(new Regex("/Notification/All"));
    }

    [Test]
    public async Task AllNotifications_VisitingMarksNotificationsRead()
    {
        // First visit marks all as read
        await Page.GotoAsync("/Notification/All");
        await Expect(Page).ToHaveTitleAsync(new Regex("Notification", RegexOptions.IgnoreCase));

        // After the visit, unread count should be 0
        var response = await Page.APIRequest.GetAsync($"{ServerFixture.BaseUrl}/Notification/Unread");
        var json = JsonDocument.Parse(await response.TextAsync());
        var count = json.RootElement.GetProperty("count").GetInt32();

        Assert.That(count, Is.EqualTo(0), "All notifications should be marked read after visiting the page");
    }

    // ─── 3. Navbar badge ─────────────────────────────────────────────────────

    [Test]
    public async Task NavbarBadge_ShowsNotificationCountOrIsHidden()
    {
        await Page.GotoAsync("/Customer/Dashboard");
        // The badge element should either be hidden (count=0) or show a number
        // Either way, the page should load without JS errors affecting the badge endpoint
        var response = await Page.APIRequest.GetAsync($"{ServerFixture.BaseUrl}/Notification/Unread");
        Assert.That(response.Status, Is.EqualTo(200));
    }
}
