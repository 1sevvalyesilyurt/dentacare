using DentalClinic.Web.Data;
using DentalClinic.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Web.Services
{
    /// <summary>
    /// Background service that runs hourly and generates in-app reminder notifications
    /// for appointments within the next 24 hours (UC-SYS01, BR-05).
    /// </summary>
    public class ReminderBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReminderBackgroundService> _logger;

        // Run reminder check every 60 minutes
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(60);

        public ReminderBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<ReminderBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ReminderBackgroundService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessRemindersAsync();
                    await Task.Delay(CheckInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected on graceful shutdown — exit the loop cleanly
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in ReminderBackgroundService.");
                    // Wait a short time before retrying to avoid a tight error loop
                    await Task.Delay(TimeSpan.FromSeconds(30), CancellationToken.None);
                }
            }

            _logger.LogInformation("ReminderBackgroundService stopping.");
        }

        private async Task ProcessRemindersAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.UtcNow;
            var cutoff = now.AddHours(24);

            // Find confirmed appointments in the next 24h that haven't been reminded yet
            var upcomingAppointments = await db.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Doctor!.User)
                .Where(a =>
                    a.Status == AppointmentStatus.Confirmed
                    && !a.ReminderSent
                    && a.AppointmentDate >= now
                    && a.AppointmentDate <= cutoff)
                .ToListAsync();

            foreach (var appointment in upcomingAppointments)
            {
                var doctorName = appointment.Doctor?.User?.FullName ?? "your doctor";
                var timeString = appointment.AppointmentDate.ToLocalTime().ToString("dddd, dd MMM yyyy 'at' HH:mm");

                var notification = new Notification
                {
                    CustomerId = appointment.CustomerId,
                    AppointmentId = appointment.AppointmentId,
                    Message = $"⏰ Reminder: You have an appointment with Dr. {doctorName} {timeString}. Please arrive 10 minutes early.",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };

                db.Notifications.Add(notification);
                appointment.ReminderSent = true;

                _logger.LogInformation(
                    "Reminder created for CustomerId={CustomerId}, AppointmentId={AppointmentId}",
                    appointment.CustomerId, appointment.AppointmentId);
            }

            if (upcomingAppointments.Any())
                await db.SaveChangesAsync();
        }
    }
}
