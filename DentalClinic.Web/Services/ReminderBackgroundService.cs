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
            var db           = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var now    = DateTime.UtcNow;
            var cutoff = now.AddHours(24);

            var upcomingAppointments = await db.Appointments
                .Include(a => a.Customer!.User)
                .Include(a => a.Doctor!.User)
                .Include(a => a.Service)
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
                var serviceName = appointment.Service?.Name ?? "";

                // In-app notification
                db.Notifications.Add(new Notification
                {
                    CustomerId    = appointment.CustomerId,
                    AppointmentId = appointment.AppointmentId,
                    Message       = $"⏰ Reminder: You have an appointment with Dr. {doctorName} {timeString}. Please arrive 10 minutes early.",
                    CreatedAt     = DateTime.UtcNow,
                    IsRead        = false
                });

                appointment.ReminderSent = true;

                // Email notification
                var patientEmail = appointment.Customer?.User?.Email;
                var patientName  = appointment.Customer?.User?.FullName ?? "Patient";
                if (!string.IsNullOrWhiteSpace(patientEmail))
                {
                    var subject = "Appointment Reminder — DentaCare Clinic";
                    var body    = BuildReminderEmailHtml(patientName, doctorName, serviceName, timeString);
                    await emailService.SendAsync(patientEmail, subject, body);
                }

                _logger.LogInformation(
                    "Reminder sent for CustomerId={CustomerId}, AppointmentId={AppointmentId}",
                    appointment.CustomerId, appointment.AppointmentId);
            }

            if (upcomingAppointments.Any())
                await db.SaveChangesAsync();
        }

        private static string BuildReminderEmailHtml(
            string patientName, string doctorName, string serviceName, string timeString) => $"""
            <html><body style="font-family:Arial,sans-serif;color:#222;">
              <h2 style="color:#0d9488;">DentaCare — Appointment Reminder</h2>
              <p>Dear <strong>{patientName}</strong>,</p>
              <p>This is a reminder that you have an upcoming appointment:</p>
              <table style="border-collapse:collapse;margin:16px 0;">
                <tr><td style="padding:6px 12px;font-weight:bold;">Doctor</td>
                    <td style="padding:6px 12px;">Dr. {doctorName}</td></tr>
                <tr style="background:#f0fdfa;"><td style="padding:6px 12px;font-weight:bold;">Service</td>
                    <td style="padding:6px 12px;">{serviceName}</td></tr>
                <tr><td style="padding:6px 12px;font-weight:bold;">Date & Time</td>
                    <td style="padding:6px 12px;">{timeString}</td></tr>
              </table>
              <p>Please arrive <strong>10 minutes early</strong>.</p>
              <p style="color:#888;font-size:12px;">DentaCare Clinic — This is an automated message.</p>
            </body></html>
            """;
    }
}
