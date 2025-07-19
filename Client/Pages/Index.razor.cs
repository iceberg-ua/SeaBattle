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

    private GameStateClient? GameState => GameStateService.GameState;
    public PlayerInfo Player => GameState?.Player!;

    public bool _gameIsOver = false;
    public string _gameOverString = string.Empty;
    public string _gameOverClass = string.Empty;

    protected override Task OnInitializedAsync()
    {
        // Single comprehensive state update handler
        BattleHub.On<GameStateClient>(nameof(IGameHub.UpdateGameState), OnUpdateGameState);
        BattleHub.On(nameof(IGameHub.GameStarted), OnGameStarted);
        BattleHub.On<bool>(nameof(IGameHub.GameOver), OnGameOver);

        return base.OnInitializedAsync();
    }

    // Computed properties based on GameState
    private bool ClearButtonDisable => GameState?.OwnField.All(c => c == CellState.empty) ?? true;
    private bool FleetComplete => GameState?.Player.State != PlayerStateEnum.Formation && GameState?.OwnField.Any(c => c == CellState.ship) == true;
    private bool IsStarted => GameState?.Stage == GameStageEnum.Game;
    private bool IsReady => GameState?.Player.State == PlayerStateEnum.Ready;

    #region Events

    private async void FieldCellClicked((int x, int y) cell)
    {
        await BattleHub?.SendAsync(nameof(IGameHub.CellClicked), Player.Id, cell.x, cell.y)!;
    }

    private async void ClearButtonClicked(MouseEventArgs e)
    {
        if (!ClearButtonDisable)
            await BattleHub?.SendAsync(nameof(IGameHub.ClearField), Player.Id)!;
    }

    private async Task OnReadyButtonClick()
    {
        if (FleetComplete)
            await BattleHub?.SendAsync(nameof(IGameHub.PlayerReady), Player.Id)!;
    }

    #endregion

    #region Hub Event Handlers

    /// <summary>
    /// Handles complete game state updates from the server.
    /// This replaces all the individual field update methods.
    /// </summary>
    private async Task OnUpdateGameState(GameStateClient updatedState)
    {
        // Update the game state through the service
        GameStateService.UpdateGameState(updatedState);
        
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

    #endregion
}