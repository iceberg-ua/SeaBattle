using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using SeaBattle.Shared;
using SeaBattle.Shared.Hub;
using SeaBattle.Shared.Player;
using SeaBattle.Client.Services;

namespace SeaBattle.Client.Pages;

public partial class Index : IDisposable
{
    [CascadingParameter]
    public HubConnection BattleHub { get; set; } = null!;

    [Inject]
    public NavigationManager Navigation { get; set; } = null!;

    [Inject]
    public IErrorHandlingService ErrorHandler { get; set; } = null!;

    [Inject]
    public INotificationService NotificationService { get; set; } = null!;

    private GameStateClient? GameState => GameStateService.GameState;
    public PlayerInfo Player => GameState?.Player!;

    public bool _gameIsOver = false;
    public string _gameOverString = string.Empty;
    public string _gameOverClass = string.Empty;
    
    // Loading states
    private bool _isClearLoading = false;
    private bool _isCellClickLoading = false;
    
    // Waiting indicator state
    private bool _showWaitingIndicator = false;

    // Rematch request state
    private bool _showRematchRequest = false;
    private string _rematchRequestPlayerName = string.Empty;
    private Guid _rematchRequestPlayerId = Guid.Empty;
    
    // Rematch requesting state (for the player who requested)
    private bool _showRematchPending = false;
    private string _rematchTargetPlayerName = string.Empty;

    // Disposal tracking
    private bool _disposed = false;
    private readonly List<IDisposable> _hubSubscriptions = new();

    protected override Task OnInitializedAsync()
    {
        // Register event handlers and track subscriptions for cleanup
        _hubSubscriptions.Add(BattleHub.On<GameStateClient>(nameof(IGameHub.UpdateGameState), OnUpdateGameState));
        _hubSubscriptions.Add(BattleHub.On(nameof(IGameHub.GameStarted), OnGameStarted));
        _hubSubscriptions.Add(BattleHub.On<bool>(nameof(IGameHub.GameOver), OnGameOver));
        _hubSubscriptions.Add(BattleHub.On<string>(nameof(IGameHub.Error), OnError));
        _hubSubscriptions.Add(BattleHub.On<Guid>(nameof(IGameHub.PlayerDisconnected), OnPlayerDisconnected));
        _hubSubscriptions.Add(BattleHub.On<string, Guid>(nameof(IGameHub.RematchRequested), OnRematchRequested));
        _hubSubscriptions.Add(BattleHub.On<bool, string>(nameof(IGameHub.RematchResponse), OnRematchResponse));
        _hubSubscriptions.Add(BattleHub.On<GameStateClient?>(nameof(IGameHub.JoinedGame), OnJoinedGame));

        // Subscribe to state refresh requests
        GameStateService.StateRefreshRequested += OnStateRefreshRequested;

        return base.OnInitializedAsync();
    }

    // Computed properties based on GameState
    private bool ClearButtonDisable => GameState?.OwnField.All(c => c == CellState.empty) ?? true;
    private bool FleetComplete => GameState?.Player.State == PlayerStateEnum.Formation && 
#if DEBUG
                                  (GameState?.FleetComplete == true || HasAtLeastOneShip);
#else
                                  GameState?.FleetComplete == true;
#endif

    private bool HasAtLeastOneShip => GameState?.OwnField.Any(c => c == CellState.ship) ?? false;
    private bool IsStarted => GameState?.Stage == GameStageEnum.Game;
    private bool IsReady => GameState?.Player.State == PlayerStateEnum.Ready;
    private bool IsGameOver => GameState?.Stage == GameStageEnum.GameOver;
    private bool ShowSetupButtons => !IsReady && !IsStarted && !IsGameOver;

    #region Events

    private async void FieldCellClicked((int x, int y) cell)
    {
        if (_isCellClickLoading) return;
        
        _isCellClickLoading = true;
        StateHasChanged();
        
        try
        {
            await BattleHub?.SendAsync(nameof(IGameHub.CellClicked), Player.Id, cell.x, cell.y)!;
        }
        finally
        {
            _isCellClickLoading = false;
            StateHasChanged();
        }
    }

    private async void ClearButtonClicked(MouseEventArgs e)
    {
        if (ClearButtonDisable || _isClearLoading) return;
        
        _isClearLoading = true;
        StateHasChanged();
        
        try
        {
            await BattleHub?.SendAsync(nameof(IGameHub.ClearField), Player.Id)!;
            NotificationService.ShowSuccess("Field Cleared", "All ships have been removed from your field.");
        }
        catch (Exception ex)
        {
            ErrorHandler.HandleSignalRError(nameof(IGameHub.ClearField), ex);
        }
        finally
        {
            _isClearLoading = false;
            StateHasChanged();
        }
    }

    private async Task OnReadyButtonClick()
    {
        Console.WriteLine($"Ready button clicked. FleetComplete: {FleetComplete}, _showWaitingIndicator: {_showWaitingIndicator}");
        
        if (!FleetComplete || _showWaitingIndicator) return;
        
        _showWaitingIndicator = true;
        Console.WriteLine($"Set _showWaitingIndicator = true. IsReady: {IsReady}, IsStarted: {IsStarted}");
        StateHasChanged();
        
        try
        {
            await BattleHub?.SendAsync(nameof(IGameHub.PlayerReady), Player.Id)!;
            Console.WriteLine("Sent PlayerReady to server");
        }
        catch (Exception ex)
        {
            ErrorHandler.HandleSignalRError(nameof(IGameHub.PlayerReady), ex);
            _showWaitingIndicator = false;
            StateHasChanged();
        }
    }

    #endregion

    #region Hub Event Handlers

    /// <summary>
    /// Handles complete game state updates from the server.
    /// This replaces all the individual field update methods.
    /// </summary>
    private async Task OnUpdateGameState(GameStateClient updatedState)
    {
        Console.WriteLine($"OnUpdateGameState: Player.State={updatedState.Player.State}, Stage={updatedState.Stage}, _showWaitingIndicator={_showWaitingIndicator}");
        
        // Update the game state through the service with validation
        bool updateSucceeded = GameStateService.UpdateGameState(updatedState);
        if (!updateSucceeded)
        {
            ErrorHandler.HandleStateError("UpdateGameState", 
                $"State validation failed for stage {updatedState.Stage}");
            GameStateService.RequestStateRefresh();
            return;
        }
        
        // Clear waiting indicator when game starts
        if (_showWaitingIndicator && updatedState.Stage == GameStageEnum.Game)
        {
            Console.WriteLine("Clearing _showWaitingIndicator - game started");
            _showWaitingIndicator = false;
        }
        
        await InvokeAsync(StateHasChanged);
    }

    private async ValueTask OnGameStarted()
    {
        // Game started notification - state update will come via OnUpdateGameState
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnGameOver(bool win)
    {
        var resultMsg = win ? "WIN" : "LOST";
        _gameOverString = $"Game over! You {resultMsg}";
        _gameOverClass = resultMsg.ToLower();
        _gameIsOver = true;

        // Show game result notification
        if (win)
        {
            NotificationService.ShowSuccess("Victory!", "Congratulations! You won the battle!");
        }
        else
        {
            NotificationService.ShowInfo("Game Over", "Better luck next time! Want to play again?");
        }

        // Final state update will come via OnUpdateGameState
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnError(string message)
    {
        // Handle different types of errors appropriately
        if (message.Contains("Invalid player ID") || message.Contains("Game not found"))
        {
            var error = new GameError(
                Code: "INVALID_GAME_STATE",
                Title: "Game Session Invalid",
                UserMessage: "Your game session is no longer valid. You'll be redirected to start a new game.",
                TechnicalDetails: message,
                Severity: ErrorSeverity.High);

            ErrorHandler.HandleError(error);
            
            // Delay navigation to let user see the message
            await Task.Delay(2000);
            Navigation.NavigateTo("/sign-in");
        }
        else
        {
            var error = new GameError(
                Code: "GAME_ERROR",
                Title: "Game Error",
                UserMessage: "An error occurred during the game. Please try again.",
                TechnicalDetails: message,
                Severity: ErrorSeverity.Medium);

            ErrorHandler.HandleError(error);
        }
        
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnPlayerDisconnected(Guid disconnectedPlayerId)
    {
        Console.WriteLine($"Player {disconnectedPlayerId} disconnected during the game");
        
        // Show a notification to the user that their opponent disconnected
        NotificationService.ShowWarning("Opponent Disconnected", 
            "Your opponent has disconnected from the game. You win by forfeit!");
        
        _gameOverString = "Opponent disconnected! You win by forfeit.";
        _gameOverClass = "win";
        _gameIsOver = true;
        
        await InvokeAsync(StateHasChanged);
    }

    private async void OnStateRefreshRequested()
    {
        try
        {
            Console.WriteLine("State refresh requested - requesting fresh state from server");
            
            // Request fresh state from the server
            if (GameState?.Player?.Id != null)
            {
                await BattleHub.SendAsync("RefreshGameState", GameState.Player.Id);
                NotificationService.ShowInfo("Refreshing Game", "Requesting updated game state from server...");
            }
        }
        catch (Exception ex)
        {
            ErrorHandler.HandleSignalRError("RefreshGameState", ex);
        }
    }

    private async Task OnRematchRequested(string requestingPlayerName, Guid requestingPlayerId)
    {
        Console.WriteLine($"OnRematchRequested: Received request from {requestingPlayerName} (ID: {requestingPlayerId})");
        
        _rematchRequestPlayerName = requestingPlayerName;
        _rematchRequestPlayerId = requestingPlayerId;
        _showRematchRequest = true;

        NotificationService.ShowInfo("Rematch Request", $"{requestingPlayerName} wants a rematch!");

        await InvokeAsync(StateHasChanged);
    }

    private async Task OnRematchResponse(bool accepted, string respondingPlayerName)
    {
        Console.WriteLine($"OnRematchResponse: {respondingPlayerName} {(accepted ? "accepted" : "declined")} rematch");
        
        // Hide pending dialog
        _showRematchPending = false;
        
        if (accepted)
        {
            NotificationService.ShowSuccess("Rematch Accepted", $"{respondingPlayerName} accepted your rematch request!");
            // Note: New game state will be sent via JoinedGame event
        }
        else
        {
            NotificationService.ShowWarning("Rematch Declined", $"{respondingPlayerName} declined your rematch request.");
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task OnJoinedGame(GameStateClient? gameState)
    {
        Console.WriteLine($"OnJoinedGame: Received new game state. Player: {gameState?.Player?.Id}");
        
        if (gameState != null)
        {
            // Update game state through the service
            bool updateSucceeded = GameStateService.UpdateGameState(gameState);
            if (!updateSucceeded)
            {
                ErrorHandler.HandleStateError("OnJoinedGame", 
                    $"Failed to update game state for new game");
                return;
            }
            
            // Clear any pending dialogs since we're starting fresh
            _showRematchRequest = false;
            _showRematchPending = false;
            _gameIsOver = false;
            
            Console.WriteLine($"OnJoinedGame: Successfully joined new game. Stage: {gameState.Stage}");
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task OnRematchButtonClick()
    {
        if (!IsGameOver)
        {
            Console.WriteLine($"OnRematchButtonClick: Game not over. IsGameOver={IsGameOver}, Stage={GameState?.Stage}");
            return;
        }

        if (Player?.Id == null)
        {
            Console.WriteLine("OnRematchButtonClick: Player ID is null");
            ErrorHandler.HandleStateError("RematchRequest", "Player information is missing");
            return;
        }

        // Find opponent name for the pending dialog
        var opponentName = "Unknown";
        if (GameState?.OpponentsName != null)
        {
            opponentName = GameState.OpponentsName;
        }

        try
        {
            Console.WriteLine($"OnRematchButtonClick: Sending rematch request from player {Player.Id}");
            await BattleHub?.SendAsync(nameof(IGameHub.RequestRematch), Player.Id)!;
            
            // Show pending request dialog
            _rematchTargetPlayerName = opponentName;
            _showRematchPending = true;
            StateHasChanged();
            
            NotificationService.ShowInfo("Rematch Request Sent", $"Rematch request sent to {opponentName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OnRematchButtonClick: Error - {ex.Message}");
            ErrorHandler.HandleSignalRError(nameof(IGameHub.RequestRematch), ex);
        }
    }

    private async Task OnNewGameButtonClick()
    {
        if (!IsGameOver)
            return;

        try
        {
            await BattleHub?.SendAsync(nameof(IGameHub.StartNewGame), Player.Id)!;
            NotificationService.ShowInfo("Starting New Game", "Looking for a new opponent...");
        }
        catch (Exception ex)
        {
            ErrorHandler.HandleSignalRError(nameof(IGameHub.StartNewGame), ex);
        }
    }

    private async Task AcceptRematchRequest()
    {
        if (!_showRematchRequest) return;

        try
        {
            await BattleHub?.SendAsync(nameof(IGameHub.RespondToRematch), Player.Id, true)!;
            _showRematchRequest = false;
            NotificationService.ShowSuccess("Rematch Accepted", "You accepted the rematch request!");
            StateHasChanged();
        }
        catch (Exception ex)
        {
            ErrorHandler.HandleSignalRError(nameof(IGameHub.RespondToRematch), ex);
        }
    }

    private async Task RejectRematchRequest()
    {
        if (!_showRematchRequest) return;

        try
        {
            await BattleHub?.SendAsync(nameof(IGameHub.RespondToRematch), Player.Id, false)!;
            _showRematchRequest = false;
            NotificationService.ShowInfo("Rematch Declined", "You declined the rematch request.");
            StateHasChanged();
        }
        catch (Exception ex)
        {
            ErrorHandler.HandleSignalRError(nameof(IGameHub.RespondToRematch), ex);
        }
    }

    private void CancelRematchRequest()
    {
        _showRematchPending = false;
        StateHasChanged();
    }


    #endregion
    
    #region Helper Methods
    
    #endregion

    #region IDisposable Implementation

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            // Clean up SignalR event handler subscriptions
            foreach (var subscription in _hubSubscriptions)
            {
                try
                {
                    subscription?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing SignalR subscription: {ex.Message}");
                }
            }
            _hubSubscriptions.Clear();

            // Clean up state service event handlers
            try
            {
                GameStateService.StateRefreshRequested -= OnStateRefreshRequested;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing state service event handler: {ex.Message}");
            }

            _disposed = true;
        }
    }

    #endregion
}