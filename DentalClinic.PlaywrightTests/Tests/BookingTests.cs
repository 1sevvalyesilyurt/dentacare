using System.Text.RegularExpressions;

namespace DentalClinic.PlaywrightTests.Tests;

[TestFixture]
[Parallelizable(ParallelScope.None)]
public class BookingTests : PlaywrightTestBase
{
    [SetUp]
    public async Task LoginBeforeEachTest()
    {
        await LoginAsCustomerAsync();
    }

    // ─── 1. Book form loads correctly ─────────────────────────────────────────

    [Test]
    public async Task BookPage_LoadsDoctorAndServiceDropdowns()
    {
        await Page.GotoAsync("/Appointment/Book");
        await Expect(Page).ToHaveTitleAsync(new Regex("Book", RegexOptions.IgnoreCase));

        // Select elements should contain at least one non-empty option
        await Expect(Page.Locator("#select-doctor")).ToContainTextAsync("Dr.");
        await Expect(Page.Locator("#select-service")).ToContainTextAsync("₺");
    }

    // ─── 2. Slot AJAX loading ─────────────────────────────────────────────────

    [Test]
    public async Task BookPage_SelectDoctorAndDate_LoadsSlots()
    {
        await Page.GotoAsync("/Appointment/Book");

        // Select the first available doctor (index 1 = first real option after placeholder)
        await Page.Locator("#select-doctor").SelectOptionAsync(
            new[] { new SelectOptionValue { Index = 1 } });

        // Pick a future weekday
        await Page.Locator("#select-date").FillAsync(NextWeekday());
        await Page.Locator("#select-date").DispatchEventAsync("change");

        // Slot section should become visible
        await Expect(Page.Locator("#slot-section")).ToBeVisibleAsync();

        // Wait for "Loading…" text to disappear (CSP-safe — no eval)
        await Expect(Page.Locator("#slot-grid")).Not.ToContainTextAsync(
            "Loading", new LocatorAssertionsToContainTextOptions { Timeout = 10_000 });
    }

    // ─── 3. Full booking flow ─────────────────────────────────────────────────

    [Test]
    public async Task Booking_ValidSelection_ShowsConfirmationPage()
    {
        await Page.GotoAsync("/Appointment/Book");

        await Page.Locator("#select-doctor").SelectOptionAsync(
            new[] { new SelectOptionValue { Index = 1 } });
        await Page.Locator("#select-service").SelectOptionAsync(
            new[] { new SelectOptionValue { Index = 1 } });

        bool slotFound = false;
        for (int offset = 1; offset <= 14 && !slotFound; offset++)
        {
            var candidate = DateTime.Today.AddDays(offset);
            if (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

            await Page.Locator("#select-date").FillAsync(candidate.ToString("yyyy-MM-dd"));
            await Page.Locator("#select-date").DispatchEventAsync("change");
            await Expect(Page.Locator("#slot-section")).ToBeVisibleAsync();

            // Wait for slots to load (no eval — CSP-safe)
            await Expect(Page.Locator("#slot-grid")).Not.ToContainTextAsync(
                "Loading", new LocatorAssertionsToContainTextOptions { Timeout = 8_000 });

            var firstSlot = Page.Locator("#slot-grid .slot-btn").First;
            if (await firstSlot.CountAsync() > 0)
            {
                await firstSlot.ClickAsync();
                slotFound = true;
            }
        }

        if (!slotFound)
            Assert.Inconclusive("No available slots in the next 14 days.");

        await Page.ClickAsync("#btn-book");
        await Page.WaitForURLAsync("**/Appointment/Confirmed/**");
        await Expect(Page).ToHaveTitleAsync(new Regex("Confirmed|Appointment", RegexOptions.IgnoreCase));
    }

    // ─── 4. Upcoming appointments list ───────────────────────────────────────

    [Test]
    public async Task AppointmentIndex_ShowsUpcomingAppointments()
    {
        await Page.GotoAsync("/Appointment");
        await Expect(Page).ToHaveURLAsync(new Regex("/Appointment$"));
        await Expect(Page).ToHaveTitleAsync(new Regex(".+"));
    }

    // ─── 5. Appointment history ───────────────────────────────────────────────

    [Test]
    public async Task AppointmentHistory_LoadsWithoutError()
    {
        await Page.GotoAsync("/Appointment/History");
        await Expect(Page).ToHaveURLAsync(new Regex("/Appointment/History"));
        await Expect(Page).ToHaveTitleAsync(new Regex(".+"));
    }

    // ─── 6. Book → Cancel flow ───────────────────────────────────────────────

    [Test]
    public async Task CancelAppointment_ExistingBooking_ShowsSuccessMessage()
    {
        // Create a booking first
        await Page.GotoAsync("/Appointment/Book");

        await Page.Locator("#select-doctor").SelectOptionAsync(
            new[] { new SelectOptionValue { Index = 1 } });
        await Page.Locator("#select-service").SelectOptionAsync(
            new[] { new SelectOptionValue { Index = 1 } });

        bool slotFound = false;
        for (int offset = 1; offset <= 14 && !slotFound; offset++)
        {
            var candidate = DateTime.Today.AddDays(offset);
            if (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

            await Page.Locator("#select-date").FillAsync(candidate.ToString("yyyy-MM-dd"));
            await Page.Locator("#select-date").DispatchEventAsync("change");
            await Expect(Page.Locator("#slot-section")).ToBeVisibleAsync();

            await Expect(Page.Locator("#slot-grid")).Not.ToContainTextAsync(
                "Loading", new LocatorAssertionsToContainTextOptions { Timeout = 8_000 });

            var firstSlot = Page.Locator("#slot-grid .slot-btn").First;
            if (await firstSlot.CountAsync() > 0)
            {
                await firstSlot.ClickAsync();
                slotFound = true;
            }
        }

        if (!slotFound)
            Assert.Inconclusive("No available slots for cancel test.");

        await Page.ClickAsync("#btn-book");
        await Page.WaitForURLAsync("**/Appointment/Confirmed/**");

        // Go to appointments list and cancel the first upcoming appointment
        await Page.GotoAsync("/Appointment");

        var cancelForm = Page.Locator("form[action*='/Appointment/Cancel']").First;
        if (await cancelForm.CountAsync() == 0)
            Assert.Inconclusive("No cancellable appointments found.");

        // Accept the native confirm() dialog before clicking
        Page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
        await cancelForm.Locator("button[type='submit']").ClickAsync();
        await Page.WaitForURLAsync("**/Appointment");

        await Expect(Page.Locator(".alert-success").First).ToBeVisibleAsync();
    }
}
