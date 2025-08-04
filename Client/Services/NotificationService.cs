using Microsoft.Extensions.Logging;

namespace SeaBattle.Client.Services;

public class NotificationService(ILogger<NotificationService> logger) : INotificationService
{
    private readonly List<Notification> _notifications = [];
    private readonly ILogger<NotificationService> _logger = logger;
    private readonly Lock _lock = new();

    public event Action<Notification>? NotificationAdded;
    public event Action<string>? NotificationRemoved;

    public IReadOnlyList<Notification> ActiveNotifications
    {
        get
        {
            lock (_lock)
            {
                ClearExpired();
                return _notifications;
            }
        }
    }

    public void ShowSuccess(string title, string message, bool autoDismiss = true)
    {
        var notification = new Notification(
            Guid.NewGuid().ToString(),
            title,
            message,
            NotificationType.Success,
            DateTime.UtcNow,
            autoDismiss);

        ShowNotification(notification);
    }

    public void ShowWarning(string title, string message, bool autoDismiss = true)
    {
        var notification = new Notification(
            Guid.NewGuid().ToString(),
            title,
            message,
            NotificationType.Warning,
            DateTime.UtcNow,
            autoDismiss);

        ShowNotification(notification);
    }

    public void ShowError(string title, string message, bool autoDismiss = false)
    {
        var notification = new Notification(
            Guid.NewGuid().ToString(),
            title,
            message,
            NotificationType.Error,
            DateTime.UtcNow,
            autoDismiss,
            autoDismiss ? 8000 : 0); // Longer display for errors

        ShowNotification(notification);
        
        _logger.LogError("User notification: {Title} - {Message}", title, message);
    }

    public void ShowInfo(string title, string message, bool autoDismiss = true)
    {
        var notification = new Notification(
            Guid.NewGuid().ToString(),
            title,
            message,
            NotificationType.Info,
            DateTime.UtcNow,
            autoDismiss);

        ShowNotification(notification);
    }

    public void ShowNotification(Notification notification)
    {
        lock (_lock)
        {
            _notifications.Add(notification);
        }

        _logger.LogDebug("Notification added: {Type} - {Title}", notification.Type, notification.Title);
        NotificationAdded?.Invoke(notification);
    }

    public void RemoveNotification(string id)
    {
        lock (_lock)
        {
            var notification = _notifications.FirstOrDefault(n => n.Id == id);
            if (notification != null)
            {
                _notifications.Remove(notification);
                _logger.LogDebug("Notification removed: {Id}", id);
                NotificationRemoved?.Invoke(id);
            }
        }
    }

    public void ClearAll()
    {
        List<string> removedIds;
        
        lock (_lock)
        {
            removedIds = _notifications.Select(n => n.Id).ToList();
            _notifications.Clear();
        }

        foreach (var id in removedIds)
        {
            NotificationRemoved?.Invoke(id);
        }

        _logger.LogDebug("All notifications cleared");
    }

    public void ClearExpired()
    {
        List<string> expiredIds;
        
        lock (_lock)
        {
            expiredIds = _notifications
                .Where(n => n.IsExpired)
                .Select(n => n.Id)
                .ToList();

            _notifications.RemoveAll(n => n.IsExpired);
        }

        foreach (var id in expiredIds)
        {
            NotificationRemoved?.Invoke(id);
        }

        if (expiredIds.Count > 0)
        {
            _logger.LogDebug("Cleared {Count} expired notifications", expiredIds.Count);
        }
    }
}