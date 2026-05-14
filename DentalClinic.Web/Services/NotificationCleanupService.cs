using DentalClinic.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Web.Services
{
    /// <summary>
    /// Background service that runs once per day and deletes read notifications
    /// older than 30 days to prevent unbounded table growth.
    /// </summary>
    public class NotificationCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NotificationCleanupService> _logger;

        private static readonly TimeSpan RunInterval  = TimeSpan.FromDays(1);
        private static readonly TimeSpan RetentionAge = TimeSpan.FromDays(30);

        public NotificationCleanupService(
            IServiceScopeFactory scopeFactory,
            ILogger<NotificationCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger       = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NotificationCleanupService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupAsync();
                    await Task.Delay(RunInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in NotificationCleanupService.");
                    await Task.Delay(TimeSpan.FromMinutes(5), CancellationToken.None);
                }
            }

            _logger.LogInformation("NotificationCleanupService stopping.");
        }

        private async Task CleanupAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var cutoff = DateTime.UtcNow - RetentionAge;

            var deleted = await db.Notifications
                .Where(n => n.IsRead && n.CreatedAt < cutoff)
                .ExecuteDeleteAsync();

            if (deleted > 0)
                _logger.LogInformation(
                    "NotificationCleanupService: deleted {Count} old notifications.", deleted);
        }
    }
}
