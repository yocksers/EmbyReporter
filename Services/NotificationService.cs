using Emby.Notifications;
using MediaBrowser.Controller.Notifications;
using MediaBrowser.Model.Logging;

namespace EmbyReporter
{
    public static class NotificationService
    {
        private static ILogger? _logger;
        private static INotificationManager? _notificationManager;
        private static bool _isRunning = false;

        public static void Start(ILogger logger, INotificationManager notificationManager)
        {
            if (_isRunning) return;

            _logger = logger;
            _notificationManager = notificationManager;
            _isRunning = true;
            _logger.Info("[NotificationService] Started.");
        }

        public static void Stop()
        {
            _isRunning = false;
            _logger?.Info("[NotificationService] Stopped.");
        }

        public static void SendPlaybackIssueNotification(string username, string itemName, string libraryName, string description)
        {
            if (!_isRunning || _notificationManager == null) return;

            var message = $"User '{username}' reported a playback issue for '{itemName}'.";
            if (!string.IsNullOrWhiteSpace(libraryName))
            {
                message += $" In library: '{libraryName}'.";
            }
            message += $" Description: {description}";

            _notificationManager.SendNotification(new Emby.Notifications.NotificationRequest
            {
                Title = "Playback Issue Reported",
                Description = message,
            });

            _logger?.Info($"[NotificationService] Sent 'Playback Issue' notification for item {itemName}.");
        }
    }
}