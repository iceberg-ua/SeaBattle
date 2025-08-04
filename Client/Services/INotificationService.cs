namespace SeaBattle.Client.Services;

public enum NotificationType
{
    Success,
    Warning,
    Error,
    Info
}

public record Notification(
    string Id,
    string Title,
    string Message,
    NotificationType Type,
    DateTime CreatedAt,
    bool AutoDismiss = true,
    int DismissAfterMs = 5000)
{
    public bool IsExpired => AutoDismiss && DateTime.UtcNow.Subtract(CreatedAt).TotalMilliseconds > DismissAfterMs;
}

public interface INotificationService
{
    event Action<Notification>? NotificationAdded;
    event Action<string>? NotificationRemoved;
    
    IReadOnlyList<Notification> ActiveNotifications { get; }
    
    void ShowSuccess(string title, string message, bool autoDismiss = true);
    void ShowWarning(string title, string message, bool autoDismiss = true);
    void ShowError(string title, string message, bool autoDismiss = false);
    void ShowInfo(string title, string message, bool autoDismiss = true);
    
    void ShowNotification(Notification notification);
    void RemoveNotification(string id);
    void ClearAll();
    void ClearExpired();
}