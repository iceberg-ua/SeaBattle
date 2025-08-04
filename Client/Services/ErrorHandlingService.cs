using Microsoft.Extensions.Logging;

namespace SeaBattle.Client.Services;

public enum ErrorSeverity
{
    Low,      // Information/warning - user can continue
    Medium,   // Action failed but recoverable
    High,     // Critical error - may need user intervention
    Critical  // Application-breaking error
}

public record GameError(
    string Code,
    string Title,
    string UserMessage,
    string? TechnicalDetails,
    ErrorSeverity Severity,
    Dictionary<string, string>? Context = null,
    Exception? Exception = null)
{
    public bool IsRetriable => Severity <= ErrorSeverity.Medium;
    public bool RequiresUserAction => Severity >= ErrorSeverity.High;
}

public interface IErrorHandlingService
{
    void HandleError(GameError error);
    void HandleException(Exception exception, string? context = null);
    void HandleSignalRError(string hubMethod, Exception exception);
    void HandleStateError(string operation, string details);
    void HandleNetworkError(string operation, Exception exception);
}

public class ErrorHandlingService : IErrorHandlingService
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<ErrorHandlingService> _logger;

    public ErrorHandlingService(
        INotificationService notificationService, 
        ILogger<ErrorHandlingService> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public void HandleError(GameError error)
    {
        // Log with appropriate level based on severity
        var logLevel = error.Severity switch
        {
            ErrorSeverity.Low => LogLevel.Information,
            ErrorSeverity.Medium => LogLevel.Warning,
            ErrorSeverity.High => LogLevel.Error,
            ErrorSeverity.Critical => LogLevel.Critical,
            _ => LogLevel.Error
        };

        _logger.Log(logLevel, error.Exception, 
            "Game error [{Code}]: {Title}. Details: {Details}. Context: {@Context}",
            error.Code, error.Title, error.TechnicalDetails, error.Context);

        // Show user notification based on severity
        switch (error.Severity)
        {
            case ErrorSeverity.Low:
                _notificationService.ShowInfo(error.Title, error.UserMessage);
                break;
            case ErrorSeverity.Medium:
                _notificationService.ShowWarning(error.Title, error.UserMessage);
                break;
            case ErrorSeverity.High:
            case ErrorSeverity.Critical:
                _notificationService.ShowError(error.Title, error.UserMessage, false);
                break;
        }
    }

    public void HandleException(Exception exception, string? context = null)
    {
        var error = new GameError(
            Code: "UNHANDLED_EXCEPTION",
            Title: "Unexpected Error",
            UserMessage: "An unexpected error occurred. Please try again or refresh the page if the problem persists.",
            TechnicalDetails: exception.Message,
            Severity: ErrorSeverity.High,
            Context: context != null ? new Dictionary<string, string> { ["Context"] = context } : null,
            Exception: exception);

        HandleError(error);
    }

    public void HandleSignalRError(string hubMethod, Exception exception)
    {
        var isConnectionError = exception.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                               exception.Message.Contains("network", StringComparison.OrdinalIgnoreCase);

        var error = new GameError(
            Code: "SIGNALR_ERROR",
            Title: isConnectionError ? "Connection Problem" : "Communication Error",
            UserMessage: isConnectionError 
                ? "Lost connection to the game server. Attempting to reconnect..."
                : "Failed to communicate with the game server. Please try again.",
            TechnicalDetails: $"Hub method: {hubMethod}, Error: {exception.Message}",
            Severity: isConnectionError ? ErrorSeverity.Medium : ErrorSeverity.High,
            Context: new Dictionary<string, string> 
            { 
                ["HubMethod"] = hubMethod,
                ["ExceptionType"] = exception.GetType().Name
            },
            Exception: exception);

        HandleError(error);
    }

    public void HandleStateError(string operation, string details)
    {
        var error = new GameError(
            Code: "STATE_ERROR",
            Title: "Game State Error",
            UserMessage: "There was a problem with the game state. The game will attempt to recover automatically.",
            TechnicalDetails: details,
            Severity: ErrorSeverity.Medium,
            Context: new Dictionary<string, string> { ["Operation"] = operation });

        HandleError(error);
    }

    public void HandleNetworkError(string operation, Exception exception)
    {
        var error = new GameError(
            Code: "NETWORK_ERROR", 
            Title: "Network Error",
            UserMessage: "Network connection problem. Please check your internet connection and try again.",
            TechnicalDetails: exception.Message,
            Severity: ErrorSeverity.Medium,
            Context: new Dictionary<string, string> { ["Operation"] = operation },
            Exception: exception);

        HandleError(error);
    }
}