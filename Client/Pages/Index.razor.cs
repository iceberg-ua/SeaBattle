using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using SeaBattle.Shared;
using SeaBattle.Shared.Hub;
using SeaBattle.Shared.Player;

namespace SeaBattle.Client.Pages;

public partial class Index
{
    [CascadingParameter]
    public HubConnection BattleHub { get; set; } = null!;

    [Inject]
    public NavigationManager Navigation { get; set; } = null!;

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

    protected override Task OnInitializedAsync()
    {
        // Single comprehensive state update handler
        BattleHub.On<GameStateClient>(nameof(IGameHub.UpdateGameState), OnUpdateGameState);
        BattleHub.On(nameof(IGameHub.GameStarted), OnGameStarted);
        BattleHub.On<bool>(nameof(IGameHub.GameOver), OnGameOver);
        BattleHub.On<string>(nameof(IGameHub.Error), OnError);
        BattleHub.On<Guid>(nameof(IGameHub.PlayerDisconnected), OnPlayerDisconnected);

        return base.OnInitializedAsync();
    }

    // Computed properties based on GameState
    private bool ClearButtonDisable => GameState?.OwnField.All(c => c == CellState.empty) ?? true;
    private bool FleetComplete => GameState?.Player.State == PlayerStateEnum.Formation && 
                                  GameState?.FleetComplete == true;
    private bool IsStarted => GameState?.Stage == GameStageEnum.Game;
    private bool IsReady => GameState?.Player.State == PlayerStateEnum.Ready;

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
            Console.WriteLine($"Error sending PlayerReady: {ex.Message}");
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
        
        // Update the game state through the service
        GameStateService.UpdateGameState(updatedState);
        
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

        // Final state update will come via OnUpdateGameState
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnError(string message)
    {
        // For now, log to console. In production, you might want to show a toast notification
        // or update the UI to display the error message
        Console.WriteLine($"Game Error: {message}");
        
        // If it's a critical connection error, redirect to sign-in
        if (message.Contains("Invalid player ID") || message.Contains("Game not found"))
        {
            Navigation.NavigateTo("/sign-in");
        }
        
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnPlayerDisconnected(Guid disconnectedPlayerId)
    {
        Console.WriteLine($"Player {disconnectedPlayerId} disconnected during the game");
        
        // Show a notification to the user that their opponent disconnected
        _gameOverString = "Opponent disconnected! You win by forfeit.";
        _gameOverClass = "win";
        _gameIsOver = true;
        
        await InvokeAsync(StateHasChanged);
    }

    #endregion
    
    #region Helper Methods
    
    private bool ShouldShowWaitingIndicator()
    {
        // Simple approach: show if we explicitly set the flag, or if player is ready but game hasn't started
        var shouldShow = _showWaitingIndicator || (IsReady && !IsStarted);
        Console.WriteLine($"ShouldShowWaitingIndicator: _showWaitingIndicator={_showWaitingIndicator}, IsReady={IsReady}, IsStarted={IsStarted}, result={shouldShow}");
        return shouldShow;
    }
    
    private string GetWaitingText()
    {
        var text = IsReady ? "Waiting for opponent..." : "Getting ready...";
        Console.WriteLine($"GetWaitingText: IsReady={IsReady}, returning='{text}'");
        return text;
    }
    
    #endregion
}